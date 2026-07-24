using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Plugins;
using CheckoutAndBuild.Core.Scripting;
using CheckoutAndBuild.Core.Services;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Common;
using CheckoutAndBuild.VisualStudio.ErrorList;
using CheckoutAndBuild.VisualStudio.Settings;

namespace CheckoutAndBuild.VisualStudio.ViewModels
{
	/// <summary>Sort orders of the solution lists (old sort context menu).</summary>
	public enum SolutionSortMode
	{
		BuildPriority,
		Name,
		Services,
		ProjectType
	}

	/// <summary>One working folder with the solutions found beneath it.</summary>
	public class WorkingFolderViewModel : NotificationObject
	{
		private readonly MainViewModel owner;

		public WorkingFolderViewModel(string path, MainViewModel owner)
		{
			Path = path;
			this.owner = owner;
			IncludedSolutions = new System.Windows.Data.ListCollectionView(Solutions)
			{
				Filter = item => ((SolutionViewModel)item).IsIncluded && owner.MatchesFilter((SolutionViewModel)item)
			};
		}

		public string Path { get; }

		public ObservableCollection<SolutionViewModel> Solutions { get; } = new ObservableCollection<SolutionViewModel>();

		/// <summary>Distinct git repositories of the solutions beneath this folder (branch selector in the header).</summary>
		public ObservableCollection<RepositoryBranchViewModel> Repositories { get; } = new ObservableCollection<RepositoryBranchViewModel>();

		/// <summary>Filtered live view of <see cref="Solutions"/> for the "Included" area.</summary>
		public ICollectionView IncludedSolutions { get; }

		/// <summary>Sorts in place by the owner's sort mode/direction.</summary>
		public void Resort()
		{
			var sorted = owner.SortSolutions(Solutions).ToList();
			for (int target = 0; target < sorted.Count; target++)
			{
				int current = Solutions.IndexOf(sorted[target]);
				if (current != target)
					Solutions.Move(current, target);
			}
		}
	}

	/// <summary>
	/// Branch display + switcher of one git repository in a working folder header
	/// (port of the old GitBranchSelector: link with the branch name, dropdown with all branches).
	/// </summary>
	public class RepositoryBranchViewModel : NotificationObject
	{
		private static readonly CheckoutAndBuild.Core.Git.GitService git = new CheckoutAndBuild.Core.Git.GitService();
		private readonly MainViewModel owner;
		private string currentBranch;

		public RepositoryBranchViewModel(string repositoryPath, bool showRepositoryName, MainViewModel owner)
		{
			RepositoryPath = repositoryPath;
			ShowRepositoryName = showRepositoryName;
			this.owner = owner;
		}

		public string RepositoryPath { get; }

		/// <summary>True when the working folder contains more than one repository ("RepoName: branch").</summary>
		public bool ShowRepositoryName { get; }

		public string RepositoryName => System.IO.Path.GetFileName(RepositoryPath);

		/// <summary>Checked-out branch; null until loaded (link stays hidden).</summary>
		public string CurrentBranch
		{
			get { return currentBranch; }
			private set
			{
				if (SetProperty(ref currentBranch, value))
				{
					RaisePropertyChanged(nameof(DisplayText));
					owner.OnRepositoryBranchChanged(this);
				}
			}
		}

		public string DisplayText => ShowRepositoryName ? $"{RepositoryName}: {CurrentBranch}" : CurrentBranch;

		/// <summary>Loads <see cref="CurrentBranch"/>; call from the UI thread (continuation updates bindings).</summary>
		public async Task LoadCurrentBranchAsync()
		{
			try
			{
				CurrentBranch = await git.GetCurrentBranchAsync(RepositoryPath);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild branch load failed: " + e.Message);
			}
		}

		/// <summary>Local branches for the dropdown (loaded on open).</summary>
		public async Task<IReadOnlyList<string>> GetBranchesAsync()
		{
			try
			{
				return await git.GetBranchesAsync(RepositoryPath);
			}
			catch (Exception e)
			{
				owner.LastError = e.Message;
				return new string[0];
			}
		}

		/// <summary>
		/// git checkout. No pre-check for uncommitted changes: a conflicting checkout fails and
		/// the git error surfaces as LastError.
		/// </summary>
		public async Task CheckoutAsync(string branch)
		{
			if (string.IsNullOrEmpty(branch) || branch == CurrentBranch)
				return;
			try
			{
				await git.CheckoutBranchAsync(RepositoryPath, branch);
				owner.LastError = null;
				CurrentBranch = branch;
			}
			catch (Exception e)
			{
				owner.LastError = e.Message;
			}
		}
	}

	/// <summary>Root view model of the CheckoutAndBuild tool window.</summary>
	public class MainViewModel : NotificationObject
	{
		private const string workingFoldersKey = "WorkingFolders";
		private const string currentProfileKey = "CurrentProfile";
		private const string profilesKey = "Profiles";
		private const string sortModeKey = "SortMode";
		private const string sortDescendingKey = "SortDescending";
		private const int maxScanDepth = 3;
		private static readonly string[] skippedDirectories = { ".git", ".vs", "bin", "obj", "node_modules", "packages" };

		private readonly Dispatcher dispatcher;
		private readonly ISettingsService settings;
		private readonly SettingsContext globalContext = new SettingsContext();
		private readonly SettingsContext profileContext = new SettingsContext();
		private string currentProfile;
		private readonly ServiceSettingsAdapter serviceSettings;
		private readonly PipelineRunner pipelineRunner = new PipelineRunner();

		private readonly PluginHost pluginHost = new PluginHost();
		private IDefaultBuildPriorityManager priorityManager;

		private readonly CleanService cleanService = new CleanService();
		private readonly GitCheckoutService checkoutService = new GitCheckoutService();
		private readonly NugetRestoreService nugetService = new NugetRestoreService();
		private readonly BuildService buildService = new BuildService();
		private readonly TestService testService = new TestService();

		private PausableCancellationTokenSource cancellation;
		private SettingsViewModel activeSettings;
		private bool isRunning;
		private bool isPaused;
		private bool loadStarted;
		private string progressText;
		private string lastProgressText;
		private double progressValue;
		private string lastError;
		private string statusMessage;
		private bool cleanEnabled;
		private bool checkoutEnabled;
		private bool restoreEnabled;
		private bool buildEnabled;
		private bool testEnabled;
		private string filterText;
		private SolutionSortMode sortMode;
		private bool sortDescending;

		private static MainViewModel shared;

		/// <summary>
		/// Shared UI instance so the tool window and the Team Explorer section show the same state.
		/// Create/access on the UI thread only (the ctor captures the current dispatcher).
		/// </summary>
		public static MainViewModel Shared => shared ?? (shared = new MainViewModel());

		public MainViewModel() : this(JsonSettingsService.CreateDefault())
		{
		}

		public MainViewModel(ISettingsService settingsService)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			settings = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

			// working profiles: list + last selection are global; profileContext scopes everything else
			Profiles = new ObservableCollection<string>(
				settings.Get<List<string>>(profilesKey, globalContext) ?? new List<string>());
			if (!Profiles.Contains(SettingsContext.DefaultProfile))
				Profiles.Insert(0, SettingsContext.DefaultProfile);
			currentProfile = settings.Get(currentProfileKey, globalContext, SettingsContext.DefaultProfile);
			if (!Profiles.Contains(currentProfile))
				currentProfile = SettingsContext.DefaultProfile;
			profileContext.Profile = currentProfile;

			serviceSettings = new ServiceSettingsAdapter(settings, profileContext);

			// default: everything on except Clean + Test (matches the old default roughly)
			cleanEnabled = settings.Get("Services.Clean", profileContext, false);
			checkoutEnabled = settings.Get("Services.Checkout", profileContext, true);
			restoreEnabled = settings.Get("Services.Restore", profileContext, true);
			buildEnabled = settings.Get("Services.Build", profileContext, true);
			testEnabled = settings.Get("Services.Test", profileContext, false);

			RunCommand = new DelegateCommand(async () => await RunPipelineAsync(),
				() => !IsRunning && AllSolutions().Any(s => s.IsIncluded && s.HasAnyServiceEnabled));
			PauseCommand = new DelegateCommand(Pause, () => IsRunning && !IsPaused);
			ResumeCommand = new DelegateCommand(Resume, () => IsRunning && IsPaused);
			CancelCommand = new DelegateCommand(() => cancellation?.Cancel(), () => IsRunning);
			AddFolderCommand = new DelegateCommand(async () => await AddFolderAsync(), () => !IsRunning);
			RemoveFolderCommand = new DelegateCommand(p => RemoveFolder(p as WorkingFolderViewModel), p => !IsRunning && p is WorkingFolderViewModel);
			RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), () => !IsRunning);
			OpenSettingsCommand = new DelegateCommand(OpenGlobalSettings);
			ExportBatchCommand = new DelegateCommand(() => ExportScript(ScriptExportType.Batch), CanExportScript);
			ExportPowershellCommand = new DelegateCommand(() => ExportScript(ScriptExportType.Powershell), CanExportScript);
			sortMode = settings.Get(sortModeKey, globalContext, SolutionSortMode.BuildPriority);
			sortDescending = settings.Get(sortDescendingKey, globalContext, false);

			AddSolutionCommand = new DelegateCommand(async p => await AddSolutionAsync(p as WorkingFolderViewModel),
				p => !IsRunning && p is WorkingFolderViewModel);
			RemoveCustomSolutionCommand = new DelegateCommand(p => RemoveCustomSolution(p as SolutionViewModel),
				p => !IsRunning && (p as SolutionViewModel)?.IsCustom == true);
			MergeFolderCommand = new DelegateCommand(p => MergeFolder(p as WorkingFolderViewModel),
				p => !IsRunning && (p as WorkingFolderViewModel)?.Solutions.Count(s => s.IsIncluded) > 1);
			SetSortModeCommand = new DelegateCommand(p => SetSortMode(p));
			OpenFolderCommand = new DelegateCommand(
				p => System.Diagnostics.Process.Start("explorer.exe", $"\"{((WorkingFolderViewModel)p).Path}\""),
				p => p is WorkingFolderViewModel);
			ToggleSortDescendingCommand = new DelegateCommand(() => { SortDescending = !SortDescending; });
			AddProfileCommand = new DelegateCommand(AddProfile, () => !IsRunning);
			RenameProfileCommand = new DelegateCommand(RenameProfile,
				() => !IsRunning && CurrentProfile != SettingsContext.DefaultProfile);
			DeleteProfileCommand = new DelegateCommand(DeleteProfile,
				() => !IsRunning && CurrentProfile != SettingsContext.DefaultProfile);
		}

		/// <summary>Error List sink; set by the tool window control (null in tests).</summary>
		internal CoabErrorListProvider ErrorSink { get; set; }

		/// <summary>Settings store, shared with the solution view models (per-solution overrides).</summary>
		internal ISettingsService Settings => settings;

		/// <summary>
		/// Settings context of the current working profile. Shared mutable instance: switching the
		/// profile updates it in place, so the solution view models and the ServiceSettingsAdapter
		/// always read/write the active profile's scope.
		/// </summary>
		internal SettingsContext ProfileContext => profileContext;

		private bool UseBranchSpecificSettings =>
			settings.Get("MiscellaneousSettings.UseBranchSpecificSettings", profileContext, false);

		/// <summary>
		/// Settings context for a solution's reads/writes: the shared profile context, or — with
		/// "Use branch specific settings" enabled — a per-call context additionally scoped to the
		/// solution's repository and its current branch (reads fall back branch → repo → global,
		/// so branch-independent values keep working until overridden on a branch).
		/// </summary>
		internal SettingsContext ContextFor(SolutionProjectModel model)
		{
			string repository = model?.GitRepositoryRoot;
			if (repository == null || !UseBranchSpecificSettings)
				return profileContext;
			string branch = WorkingFolders.SelectMany(f => f.Repositories)
				.FirstOrDefault(r => string.Equals(r.RepositoryPath, repository, StringComparison.OrdinalIgnoreCase))
				?.CurrentBranch;
			if (string.IsNullOrEmpty(branch))
				return profileContext;
			return new SettingsContext { Profile = profileContext.Profile, RepositoryPath = repository, Branch = branch };
		}

		/// <summary>Re-reads the branch-scoped state of the repository's solutions after a branch load/switch.</summary>
		internal void OnRepositoryBranchChanged(RepositoryBranchViewModel repository)
		{
			if (!UseBranchSpecificSettings)
				return;
			foreach (var solution in AllSolutions()
				.Where(s => string.Equals(s.Model.GitRepositoryRoot, repository.RepositoryPath, StringComparison.OrdinalIgnoreCase))
				.ToList())
			{
				ReloadSolutionScopedState(solution);
			}
			foreach (var folder in WorkingFolders.Where(f => f.Repositories.Contains(repository)))
			{
				folder.Resort();
				folder.IncludedSolutions.Refresh();
			}
		}

		public ObservableCollection<WorkingFolderViewModel> WorkingFolders { get; } = new ObservableCollection<WorkingFolderViewModel>();

		/// <summary>All working profile names; always contains at least "Default".</summary>
		public ObservableCollection<string> Profiles { get; }

		/// <summary>
		/// Active working profile. Every solution-related setting (included, priority, services,
		/// build properties/targets, step flags, SettingsProperty values) is scoped by it;
		/// the working folders themselves are shared between profiles.
		/// </summary>
		public string CurrentProfile
		{
			get { return currentProfile; }
			set
			{
				if (string.IsNullOrEmpty(value) || value == currentProfile)
					return;
				if (IsRunning || !Profiles.Contains(value))
				{
					// snap the ComboBox back to the real value
					dispatcher.BeginInvoke(new Action(() => RaisePropertyChanged(nameof(CurrentProfile))));
					return;
				}
				if (SetProperty(ref currentProfile, value))
				{
					settings.Set(currentProfileKey, globalContext, value);
					profileContext.Profile = value;
					ReloadProfileScopedState();
					CommandManager.InvalidateRequerySuggested();
				}
			}
		}

		/// <summary>Flat list of all excluded solutions across all working folders ("Excluded" area).</summary>
		public ObservableCollection<SolutionViewModel> ExcludedSolutions { get; } = new ObservableCollection<SolutionViewModel>();

		public ICommand RunCommand { get; }
		public ICommand PauseCommand { get; }
		public ICommand ResumeCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand AddFolderCommand { get; }
		public ICommand RemoveFolderCommand { get; }
		public ICommand RefreshCommand { get; }
		public ICommand OpenSettingsCommand { get; }
		public ICommand ExportBatchCommand { get; }
		public ICommand ExportPowershellCommand { get; }
		public ICommand AddProfileCommand { get; }
		public ICommand RenameProfileCommand { get; }
		public ICommand DeleteProfileCommand { get; }
		public ICommand AddSolutionCommand { get; }
		public ICommand RemoveCustomSolutionCommand { get; }
		public ICommand MergeFolderCommand { get; }
		public ICommand OpenFolderCommand { get; }
		public ICommand SetSortModeCommand { get; }
		public ICommand ToggleSortDescendingCommand { get; }

		#region filter + sort

		/// <summary>Filter over the solution names (search box, Ctrl+E).</summary>
		public string FilterText
		{
			get { return filterText; }
			set
			{
				if (SetProperty(ref filterText, value))
				{
					foreach (var folder in WorkingFolders)
						folder.IncludedSolutions.Refresh();
				}
			}
		}

		internal bool MatchesFilter(SolutionViewModel solution) =>
			string.IsNullOrEmpty(filterText)
			|| solution.SolutionFileName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;

		public SolutionSortMode SortMode
		{
			get { return sortMode; }
			set
			{
				if (SetProperty(ref sortMode, value))
				{
					settings.Set(sortModeKey, globalContext, value);
					RaiseSortFlags();
					ResortAll();
				}
			}
		}

		public bool SortDescending
		{
			get { return sortDescending; }
			set
			{
				if (SetProperty(ref sortDescending, value))
				{
					settings.Set(sortDescendingKey, globalContext, value);
					ResortAll();
				}
			}
		}

		// checkable menu flags (avoids an enum converter in XAML)
		public bool IsSortByPriority => sortMode == SolutionSortMode.BuildPriority;
		public bool IsSortByName => sortMode == SolutionSortMode.Name;
		public bool IsSortByServices => sortMode == SolutionSortMode.Services;
		public bool IsSortByProjectType => sortMode == SolutionSortMode.ProjectType;

		private void RaiseSortFlags()
		{
			RaisePropertyChanged(nameof(IsSortByPriority));
			RaisePropertyChanged(nameof(IsSortByName));
			RaisePropertyChanged(nameof(IsSortByServices));
			RaisePropertyChanged(nameof(IsSortByProjectType));
		}

		private void SetSortMode(object parameter)
		{
			if (parameter is string name && Enum.TryParse(name, out SolutionSortMode mode))
				SortMode = mode;
		}

		private void ResortAll()
		{
			foreach (var folder in WorkingFolders)
				folder.Resort();
		}

		/// <summary>Sort order used by the working folder lists (secondary key: name).</summary>
		internal IEnumerable<SolutionViewModel> SortSolutions(IEnumerable<SolutionViewModel> solutions)
		{
			IOrderedEnumerable<SolutionViewModel> ordered;
			switch (sortMode)
			{
				case SolutionSortMode.Name:
					ordered = solutions.OrderBy(s => s.SolutionFileName, StringComparer.OrdinalIgnoreCase);
					break;
				case SolutionSortMode.Services:
					ordered = solutions.OrderBy(s => s.ServicesCaption, StringComparer.OrdinalIgnoreCase);
					break;
				case SolutionSortMode.ProjectType:
					ordered = solutions.OrderBy(s => s.Model.IsDelphiProject ? 1 : 0);
					break;
				default:
					ordered = solutions.OrderBy(s => s.BuildPriority);
					break;
			}
			var result = ordered.ThenBy(s => s.SolutionFileName, StringComparer.OrdinalIgnoreCase);
			return sortDescending ? result.Reverse() : (IEnumerable<SolutionViewModel>)result;
		}

		#endregion

		/// <summary>Non-null while the settings "page" is shown instead of the main content.</summary>
		public SettingsViewModel ActiveSettings
		{
			get { return activeSettings; }
			private set { SetProperty(ref activeSettings, value); }
		}

		/// <summary>Opens the global settings editor (gear button, Tools → Options page).</summary>
		public void OpenGlobalSettings()
		{
			var viewModel = new SettingsViewModel(settings, "Settings", null, CloseSettings,
				SettingsUiFactory.GetSettingsClasses(pluginHost), CurrentProfile);
			if (settings is JsonSettingsService jsonSettings)
				viewModel.Maintenance = new MaintenanceViewModel(jsonSettings,
					() => Profiles.ToList(), () => CurrentProfile, ReloadAfterStoreChange);
			viewModel.Plugins = new PluginsViewModel(pluginHost.Errors);
			ActiveSettings = viewModel;
		}

		/// <summary>Re-reads everything from the store after an import/copy/reset.</summary>
		private void ReloadAfterStoreChange()
		{
			Profiles.Clear();
			foreach (string profile in settings.Get<List<string>>(profilesKey, globalContext) ?? new List<string>())
				Profiles.Add(profile);
			if (!Profiles.Contains(SettingsContext.DefaultProfile))
				Profiles.Insert(0, SettingsContext.DefaultProfile);
			string stored = settings.Get(currentProfileKey, globalContext, SettingsContext.DefaultProfile);
			currentProfile = Profiles.Contains(stored) ? stored : SettingsContext.DefaultProfile;
			profileContext.Profile = currentProfile;
			RaisePropertyChanged(nameof(CurrentProfile));
			ReloadProfileScopedState();
		}

		/// <summary>Opens the solution-scoped settings editor (solution context menu).</summary>
		internal void OpenSolutionSettings(SolutionViewModel solution)
		{
			ActiveSettings = new SettingsViewModel(settings, $"Settings — {solution.SolutionFileName}", solution.ItemPath, CloseSettings,
				SettingsUiFactory.GetSettingsClasses(pluginHost), CurrentProfile);
		}

		private void CloseSettings()
		{
			ActiveSettings = null;
			// options may have changed (e.g. "Use branch specific settings"): re-read the scoped state
			ReloadProfileScopedState();
		}

		/// <summary>Re-reads a solution's scoped values (profile + optional branch scope).</summary>
		private void ReloadSolutionScopedState(SolutionViewModel solution)
		{
			var context = ContextFor(solution.Model);
			solution.IsIncluded = settings.Get($"IsIncluded:{solution.ItemPath}", context, true);
			solution.BuildPriority = GetInitialBuildPriority(solution.Model, context);
			solution.ReloadProfileScopedState();
		}

		internal IOperationService CleanOperation => cleanService;
		internal IOperationService BuildOperation => buildService;
		internal IOperationService TestOperation => testService;

		public bool IsRunning
		{
			get { return isRunning; }
			private set
			{
				if (SetProperty(ref isRunning, value))
					CommandManager.InvalidateRequerySuggested();
			}
		}

		public bool IsPaused
		{
			get { return isPaused; }
			private set
			{
				if (SetProperty(ref isPaused, value))
					CommandManager.InvalidateRequerySuggested();
			}
		}

		public string ProgressText
		{
			get { return progressText; }
			private set { SetProperty(ref progressText, value); }
		}

		public double ProgressValue
		{
			get { return progressValue; }
			private set { SetProperty(ref progressValue, value); }
		}

		public string LastError
		{
			get { return lastError; }
			internal set { SetProperty(ref lastError, value); }
		}

		/// <summary>Neutral status line (e.g. "Exported: c:\...\CheckoutAndBuild.bat").</summary>
		public string StatusMessage
		{
			get { return statusMessage; }
			private set { SetProperty(ref statusMessage, value); }
		}

		public bool IsCleanEnabled
		{
			get { return cleanEnabled; }
			set { if (SetProperty(ref cleanEnabled, value)) { settings.Set("Services.Clean", profileContext, value); RefreshSolutionServiceFlags(); } }
		}

		public bool IsCheckoutEnabled
		{
			get { return checkoutEnabled; }
			set { if (SetProperty(ref checkoutEnabled, value)) { settings.Set("Services.Checkout", profileContext, value); RefreshSolutionServiceFlags(); } }
		}

		public bool IsRestoreEnabled
		{
			get { return restoreEnabled; }
			set { if (SetProperty(ref restoreEnabled, value)) { settings.Set("Services.Restore", profileContext, value); RefreshSolutionServiceFlags(); } }
		}

		public bool IsBuildEnabled
		{
			get { return buildEnabled; }
			set { if (SetProperty(ref buildEnabled, value)) { settings.Set("Services.Build", profileContext, value); RefreshSolutionServiceFlags(); } }
		}

		public bool IsTestEnabled
		{
			get { return testEnabled; }
			set { if (SetProperty(ref testEnabled, value)) { settings.Set("Services.Test", profileContext, value); RefreshSolutionServiceFlags(); } }
		}

		/// <summary>Global step checkboxes are the fallback of the per-solution flags: re-raise them all.</summary>
		private void RefreshSolutionServiceFlags()
		{
			foreach (var solution in AllSolutions())
				solution.RefreshServiceFlags();
		}

		/// <summary>Loads the persisted working folders and scans them. Safe to call multiple times.</summary>
		public async Task LoadAsync()
		{
			if (loadStarted)
				return;
			loadStarted = true;
			await LoadPluginsAsync();
			try
			{
				var instances = await Task.Run(() => VsWhere.GetInstances());
				foreach (var instance in instances)
					VsInstances.Add(instance);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild vswhere failed: " + e.Message);
			}
			try
			{
				var folders = settings.Get<List<string>>(workingFoldersKey, globalContext) ?? new List<string>();
				foreach (string folder in folders.Where(Directory.Exists))
					await AddFolderCoreAsync(folder);
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		/// <summary>
		/// Loads MEF plugins from &lt;ExtensionDir&gt;\Plugins plus the optional "PluginDirectories"
		/// setting (semicolon-separated) on a background thread; failures only go to the trace log.
		/// </summary>
		private async Task LoadPluginsAsync()
		{
			try
			{
				var directories = new List<string>();
				string extensionDir = Path.GetDirectoryName(typeof(MainViewModel).Assembly.Location);
				if (!string.IsNullOrEmpty(extensionDir))
					directories.Add(Path.Combine(extensionDir, "Plugins"));
				string configured = settings.Get<string>("PluginDirectories", globalContext);
				if (!string.IsNullOrEmpty(configured))
					directories.AddRange(configured.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim()));

				IServiceProvider hostServices = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;
				await Task.Run(() => pluginHost.LoadAsync(directories, hostServices));

				buildService.BuildPropertiesProviders = pluginHost.GetExportedValues<IProjectBuildPropertiesProvider>().ToList();
				priorityManager = pluginHost.GetExportedValues<IDefaultBuildPriorityManager>().FirstOrDefault();

				foreach (string error in pluginHost.Errors)
					System.Diagnostics.Trace.WriteLine("CheckoutAndBuild plugin load: " + error);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild plugin load failed: " + e.Message);
			}
		}

		#region working profiles

		private void AddProfile()
		{
			string name = PromptForProfileName("Add Profile", string.Empty);
			if (name == null)
				return;
			string existing = Profiles.FirstOrDefault(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
			if (existing == null)
			{
				Profiles.Add(name);
				PersistProfiles();
			}
			CurrentProfile = existing ?? name;
		}

		private void RenameProfile()
		{
			string oldName = CurrentProfile;
			if (oldName == SettingsContext.DefaultProfile)
				return;
			string name = PromptForProfileName("Rename Profile", oldName);
			if (name == null || name == oldName
				|| Profiles.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
				return;

			settings.RenameProfile(oldName, name);
			Profiles[Profiles.IndexOf(oldName)] = name;
			PersistProfiles();
			// keys were moved along, so no ReloadProfileScopedState needed
			currentProfile = name;
			profileContext.Profile = name;
			settings.Set(currentProfileKey, globalContext, name);
			RaisePropertyChanged(nameof(CurrentProfile));
		}

		private void DeleteProfile()
		{
			string name = CurrentProfile;
			if (name == SettingsContext.DefaultProfile)
				return;
			if (MessageBox.Show($"Delete profile '{name}'?", "CheckoutAndBuild",
					MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;

			// ponytail: lazy delete — the profile's settings stay in the store, only the list entry goes
			CurrentProfile = SettingsContext.DefaultProfile;
			Profiles.Remove(name);
			PersistProfiles();
		}

		private void PersistProfiles()
		{
			settings.Set(profilesKey, globalContext, Profiles.ToList());
		}

		/// <summary>
		/// Re-reads all profile-scoped values (step flags + per-solution state) after a profile
		/// switch. No file rescan: the solutions stay, only their stored settings are re-read.
		/// </summary>
		private void ReloadProfileScopedState()
		{
			cleanEnabled = settings.Get("Services.Clean", profileContext, false);
			checkoutEnabled = settings.Get("Services.Checkout", profileContext, true);
			restoreEnabled = settings.Get("Services.Restore", profileContext, true);
			buildEnabled = settings.Get("Services.Build", profileContext, true);
			testEnabled = settings.Get("Services.Test", profileContext, false);
			RaisePropertyChanged(nameof(IsCleanEnabled));
			RaisePropertyChanged(nameof(IsCheckoutEnabled));
			RaisePropertyChanged(nameof(IsRestoreEnabled));
			RaisePropertyChanged(nameof(IsBuildEnabled));
			RaisePropertyChanged(nameof(IsTestEnabled));

			foreach (var solution in AllSolutions().ToList())
				ReloadSolutionScopedState(solution);
			foreach (var folder in WorkingFolders)
			{
				folder.Resort();
				folder.IncludedSolutions.Refresh();
			}
		}

		/// <summary>Small modal name editor (analog to the solution option dialogs). Returns null on cancel/empty.</summary>
		private static string PromptForProfileName(string title, string initialValue)
		{
			var textBox = new TextBox { Text = initialValue, Margin = new Thickness(8, 4, 8, 0) };
			var ok = new Button { Content = "OK", Width = 72, Margin = new Thickness(0, 8, 8, 8), IsDefault = true };
			var cancel = new Button { Content = "Cancel", Width = 72, Margin = new Thickness(0, 8, 8, 8), IsCancel = true };
			var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			buttons.Children.Add(ok);
			buttons.Children.Add(cancel);
			var panel = new StackPanel();
			panel.Children.Add(new TextBlock { Text = "Profile name:", Margin = new Thickness(8, 8, 8, 0), Opacity = 0.7 });
			panel.Children.Add(textBox);
			panel.Children.Add(buttons);

			var window = new Window
			{
				Title = title,
				Content = panel,
				Width = 340,
				SizeToContent = SizeToContent.Height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
			window.Loaded += (s, e) => { textBox.Focus(); textBox.SelectAll(); };
			ok.Click += (s, e) => window.DialogResult = true;
			if (window.ShowDialog() != true)
				return null;

			// "$" is the settings key separator and must not appear in a profile name
			string name = textBox.Text?.Trim().Replace("$", string.Empty);
			return string.IsNullOrEmpty(name) ? null : name;
		}

		#endregion

		#region working folders

		private async Task AddFolderAsync()
		{
			string path;
			using (var dialog = new System.Windows.Forms.FolderBrowserDialog
			{
				Description = "Select a working folder to scan for solutions (*.sln)"
			})
			{
				if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
					return;
				path = dialog.SelectedPath;
			}

			if (string.IsNullOrEmpty(path)
				|| WorkingFolders.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
				return;

			try
			{
				await AddFolderCoreAsync(path);
				PersistFolders();
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		private void RemoveFolder(WorkingFolderViewModel folder)
		{
			if (folder == null)
				return;
			DetachSolutions(folder);
			WorkingFolders.Remove(folder);
			PersistFolders();
		}

		private async Task RefreshAsync()
		{
			try
			{
				var paths = WorkingFolders.Select(f => f.Path).ToList();
				foreach (var folder in WorkingFolders)
					DetachSolutions(folder);
				WorkingFolders.Clear();
				foreach (string path in paths)
					await AddFolderCoreAsync(path);
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		private async Task AddFolderCoreAsync(string path)
		{
			var folder = new WorkingFolderViewModel(path, this);
			WorkingFolders.Add(folder);

			var customPaths = settings.Get<List<string>>(CustomSolutionsKey(path), globalContext) ?? new List<string>();
			var models = await Task.Run(() =>
			{
				var found = ScanForSolutions(path);
				var scannedPaths = new HashSet<string>(found.Select(m => m.ItemPath), StringComparer.OrdinalIgnoreCase);
				foreach (string custom in customPaths.Where(File.Exists).Where(p => !scannedPaths.Contains(p)))
				{
					try
					{
						found.Add(SolutionParser.Parse(custom));
					}
					catch (Exception)
					{
						// unparsable custom solution: skip it
					}
				}
				foreach (var model in found)
				{
					// branch not loaded yet at this point: read profile-scoped; OnRepositoryBranchChanged re-applies
					model.IsIncluded = settings.Get($"IsIncluded:{model.ItemPath}", profileContext, true);
					model.BuildPriority = GetInitialBuildPriority(model, profileContext);
				}
				return found;
			});

			foreach (var model in models)
			{
				var solution = new SolutionViewModel(model, this, dispatcher)
				{
					IsCustom = customPaths.Contains(model.ItemPath, StringComparer.OrdinalIgnoreCase)
				};
				solution.PropertyChanged += OnSolutionPropertyChanged;
				folder.Solutions.Add(solution);
				if (!solution.IsIncluded)
					InsertExcluded(solution);
			}
			folder.Resort();

			// branch selector: distinct repos of the folder's solutions; branches load async
			var repositoryRoots = models.Select(m => m.GitRepositoryRoot)
				.Where(r => r != null)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			foreach (string root in repositoryRoots)
			{
				var repository = new RepositoryBranchViewModel(root, repositoryRoots.Count > 1, this);
				folder.Repositories.Add(repository);
				_ = repository.LoadCurrentBranchAsync();
			}
		}

		/// <summary>Stored priority wins; without one, a plugin IDefaultBuildPriorityManager may supply the default.</summary>
		private int GetInitialBuildPriority(ISolutionProjectModel model, SettingsContext context)
		{
			int stored = settings.Get($"BuildPriority:{model.ItemPath}", context, int.MinValue);
			if (stored != int.MinValue)
				return stored;
			try
			{
				return priorityManager?.GetDefaultBuildPriority(model) ?? 0;
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild priority manager failed: " + e.Message);
				return 0;
			}
		}

		private void DetachSolutions(WorkingFolderViewModel folder)
		{
			foreach (var solution in folder.Solutions)
			{
				solution.PropertyChanged -= OnSolutionPropertyChanged;
				solution.Detach();
				ExcludedSolutions.Remove(solution);
			}
		}

		private void PersistFolders()
		{
			settings.Set(workingFoldersKey, globalContext, WorkingFolders.Select(f => f.Path).ToList());
		}

		private static string CustomSolutionsKey(string folderPath) => "CustomSolutions:" + folderPath;

		/// <summary>Adds solutions outside the folder scan via multi-select file dialog (old AddSolution).</summary>
		private async Task AddSolutionAsync(WorkingFolderViewModel folder)
		{
			if (folder == null)
				return;
			var dialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Solution files|*.sln",
				Multiselect = true,
				Title = "Add solutions to " + folder.Path
			};
			if (dialog.ShowDialog() != true)
				return;

			var custom = settings.Get<List<string>>(CustomSolutionsKey(folder.Path), globalContext) ?? new List<string>();
			var existing = new HashSet<string>(folder.Solutions.Select(s => s.ItemPath), StringComparer.OrdinalIgnoreCase);
			bool added = false;
			foreach (string file in dialog.FileNames.Where(f => !existing.Contains(f)))
			{
				SolutionProjectModel model;
				try
				{
					model = await Task.Run(() => SolutionParser.Parse(file));
				}
				catch (Exception e)
				{
					LastError = $"{System.IO.Path.GetFileName(file)}: {e.Message}";
					continue;
				}
				model.IsIncluded = settings.Get($"IsIncluded:{model.ItemPath}", profileContext, true);
				model.BuildPriority = GetInitialBuildPriority(model, profileContext);
				var solution = new SolutionViewModel(model, this, dispatcher) { IsCustom = true };
				solution.PropertyChanged += OnSolutionPropertyChanged;
				folder.Solutions.Add(solution);
				if (!solution.IsIncluded)
					InsertExcluded(solution);
				custom.Add(file);
				added = true;
			}
			if (added)
			{
				settings.Set(CustomSolutionsKey(folder.Path), globalContext, custom);
				folder.Resort();
				folder.IncludedSolutions.Refresh();
			}
		}

		/// <summary>Removes a manually added solution from its folder list again.</summary>
		internal void RemoveCustomSolution(SolutionViewModel solution)
		{
			if (solution == null)
				return;
			var folder = WorkingFolders.FirstOrDefault(f => f.Solutions.Contains(solution));
			if (folder == null)
				return;
			solution.PropertyChanged -= OnSolutionPropertyChanged;
			solution.Detach();
			folder.Solutions.Remove(solution);
			ExcludedSolutions.Remove(solution);

			var custom = settings.Get<List<string>>(CustomSolutionsKey(folder.Path), globalContext) ?? new List<string>();
			custom.RemoveAll(p => string.Equals(p, solution.ItemPath, StringComparison.OrdinalIgnoreCase));
			settings.Set(CustomSolutionsKey(folder.Path), globalContext, custom);
		}

		/// <summary>Merges all included solutions of the folder into one !Merged_Build_*.sln (old "Merge to One Solution").</summary>
		private void MergeFolder(WorkingFolderViewModel folder)
		{
			if (folder == null)
				return;
			var paths = folder.Solutions.Where(s => s.IsIncluded).Select(s => s.ItemPath).ToList();
			if (paths.Count < 2)
				return;
			try
			{
				string output = System.IO.Path.Combine(folder.Path,
					"!Merged_Build_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".sln");
				CheckoutAndBuild.Core.Merge.SolutionMerger.Merge(paths, output);
				LastError = null;
				StatusMessage = "Merged solution: " + output;
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		/// <summary>Installed VS instances for the "Open with…" submenu (loaded once in LoadAsync).</summary>
		public ObservableCollection<VsInstance> VsInstances { get; } = new ObservableCollection<VsInstance>();

		private void OnSolutionPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var solution = (SolutionViewModel)sender;
			if (e.PropertyName == nameof(SolutionViewModel.IsIncluded))
			{
				settings.Set($"IsIncluded:{solution.ItemPath}", ContextFor(solution.Model), solution.IsIncluded);
				if (solution.IsIncluded)
					ExcludedSolutions.Remove(solution);
				else if (!ExcludedSolutions.Contains(solution))
					InsertExcluded(solution);
				WorkingFolders.FirstOrDefault(f => f.Solutions.Contains(solution))?.IncludedSolutions.Refresh();
			}
			else if (e.PropertyName == nameof(SolutionViewModel.BuildPriority))
			{
				settings.Set($"BuildPriority:{solution.ItemPath}", ContextFor(solution.Model), solution.BuildPriority);
				WorkingFolders.FirstOrDefault(f => f.Solutions.Contains(solution))?.Resort();
			}
		}

		/// <summary>Inserts alphabetically into the flat excluded list.</summary>
		private void InsertExcluded(SolutionViewModel solution)
		{
			int index = 0;
			while (index < ExcludedSolutions.Count
				&& string.Compare(ExcludedSolutions[index].SolutionFileName, solution.SolutionFileName, StringComparison.OrdinalIgnoreCase) < 0)
				index++;
			ExcludedSolutions.Insert(index, solution);
		}

		private static List<SolutionProjectModel> ScanForSolutions(string root)
		{
			var result = new List<SolutionProjectModel>();
			Scan(root, 0);
			return result;

			void Scan(string directory, int depth)
			{
				try
				{
					foreach (string sln in Directory.EnumerateFiles(directory, "*.sln"))
					{
						try
						{
							result.Add(SolutionParser.Parse(sln));
						}
						catch (Exception)
						{
							// unparsable solution: skip it
						}
					}

					if (depth >= maxScanDepth)
						return;
					foreach (string sub in Directory.EnumerateDirectories(directory))
					{
						if (skippedDirectories.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
							continue;
						Scan(sub, depth + 1);
					}
				}
				catch (UnauthorizedAccessException)
				{
				}
				catch (IOException)
				{
				}
			}
		}

		#endregion

		#region pipeline execution

		private void Pause() => cancellation?.Pause();

		private void Resume() => cancellation?.Resume();

		/// <summary>
		/// PausableCancellationTokenSource.PausedChanged: updates IsPaused and re-raises the status
		/// of every solution so busy rows show "Paused" (orange) while the pipeline is held.
		/// </summary>
		private void OnPausedChanged(object sender, bool paused)
		{
			if (!dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke(new Action(() => OnPausedChanged(sender, paused)));
				return;
			}
			IsPaused = paused;
			ProgressText = paused ? "Paused" : lastProgressText;
			foreach (var solution in AllSolutions())
				solution.RefreshResult();
		}

		private async Task RunPipelineAsync()
		{
			await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			if (IsRunning)
				return;
			var solutions = AllSolutions().ToList();
			var models = solutions.Select(s => (ISolutionProjectModel)s.Model).ToList();

			// per-service solution sets: solution overrides win over the global step checkboxes
			var enabledModels = AllServices().ToDictionary(
				service => service,
				service => new HashSet<ISolutionProjectModel>(solutions
					.Where(s => s.IsIncluded && IsServiceEnabledFor(service, s))
					.Select(s => (ISolutionProjectModel)s.Model)));
			var services = AllServices().Where(service => enabledModels[service].Count > 0).ToList();
			if (models.Count == 0 || services.Count == 0)
				return;

			LastError = null;
			StatusMessage = null;
			ErrorSink?.Clear();
			IsRunning = true;
			IsPaused = false;
			cancellation = new PausableCancellationTokenSource();
			cancellation.PausedChanged += OnPausedChanged;
			var context = new PipelineContext
			{
				Settings = serviceSettings,
				PreBuildScript = serviceSettings.PreBuildScriptPath,
				PostBuildScript = serviceSettings.PostBuildScriptPath,
				Progress = new DelegateProgress(OnPipelineProgress),
				CustomActions = pluginHost.GetExportedValues<ICustomAction>().ToList(),
				ServiceProjectFilter = (service, model) =>
					!enabledModels.TryGetValue(service, out var enabled) || enabled.Contains(model)
			};

			try
			{
				await Task.Run(() => pipelineRunner.RunAsync(models, services, context, cancellation));
				ProgressText = cancellation.IsCancellationRequested ? "Cancelled" : "Done";
			}
			catch (OperationCanceledException)
			{
				ProgressText = "Cancelled";
			}
			catch (Exception e)
			{
				LastError = e.Message;
				ProgressText = "Failed";
			}
			finally
			{
				FinishRun();
			}
		}

		/// <summary>Runs a single service for a single solution (context menu: build/clean/test only).</summary>
		internal async Task RunSingleServiceAsync(SolutionViewModel solution, IOperationService service)
		{
			await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			if (IsRunning)
				return;

			LastError = null;
			StatusMessage = null;
			ErrorSink?.Clear();
			IsRunning = true;
			IsPaused = false;
			cancellation = new PausableCancellationTokenSource();
			cancellation.PausedChanged += OnPausedChanged;
			ProgressText = lastProgressText = $"{service.OperationName}: {solution.SolutionFileName}";
			ProgressValue = 0;

			try
			{
				await Task.Run(() => service.ExecuteAsync(new[] { (ISolutionProjectModel)solution.Model }, serviceSettings, cancellation));
				ProgressText = cancellation.IsCancellationRequested ? "Cancelled" : "Done";
			}
			catch (OperationCanceledException)
			{
				ProgressText = "Cancelled";
			}
			catch (Exception e)
			{
				LastError = e.Message;
				ProgressText = "Failed";
			}
			finally
			{
				FinishRun();
			}
		}

		private void FinishRun()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			cancellation?.Dispose();
			cancellation = null;
			IsRunning = false;
			IsPaused = false;
			ProgressValue = 0;
			foreach (var solution in AllSolutions())
				solution.RefreshResult();
			ReportResultsToErrorList();
		}

		/// <summary>Pushes the failures of the last run into the VS Error List (no-op without a sink).</summary>
		private void ReportResultsToErrorList()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			if (ErrorSink == null)
				return;
			try
			{
				foreach (var solution in AllSolutions())
				{
					// ponytail: Model.Result only keeps the LAST operation's result, so build errors
					// vanish from the list when tests ran afterwards; keep per-service results if that hurts
					switch (solution.Model.Result)
					{
						case BuildResult build when build.Errors.Count > 0:
							ErrorSink.Report(build.Errors);
							break;
						case TestRunResult tests when tests.Failures.Count > 0:
							ErrorSink.ReportTestFailures(solution.SolutionFileName, tests.Failures);
							break;
					}
				}
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		private void OnPipelineProgress(PipelineProgress progress)
		{
			dispatcher.BeginInvoke(new Action(() =>
			{
				lastProgressText = $"{progress.OperationName} ({progress.ServiceIndex + 1}/{progress.ServiceCount})";
				ProgressText = IsPaused ? "Paused" : lastProgressText;
				if (progress.ServiceCount > 0)
					ProgressValue = (progress.ServiceIndex + 1) * 100.0 / progress.ServiceCount;
				if (!string.IsNullOrEmpty(progress.Error))
					LastError = progress.Error;
			}));
		}

		private IEnumerable<SolutionViewModel> AllSolutions() => WorkingFolders.SelectMany(f => f.Solutions);

		private IEnumerable<IOperationService> EnabledServices()
		{
			if (IsCleanEnabled) yield return cleanService;
			if (IsCheckoutEnabled) yield return checkoutService;
			if (IsRestoreEnabled) yield return nugetService;
			if (IsBuildEnabled) yield return buildService;
			if (IsTestEnabled) yield return testService;
		}

		private IEnumerable<IOperationService> AllServices()
		{
			yield return cleanService;
			yield return checkoutService;
			yield return nugetService;
			yield return buildService;
			yield return testService;
		}

		/// <summary>Effective (solution-override or global) enable flag of a service for a solution.</summary>
		private bool IsServiceEnabledFor(IOperationService service, SolutionViewModel solution)
		{
			if (ReferenceEquals(service, cleanService)) return solution.IsCleanEnabled;
			if (ReferenceEquals(service, checkoutService)) return solution.IsCheckoutEnabled;
			if (ReferenceEquals(service, nugetService)) return solution.IsRestoreEnabled;
			if (ReferenceEquals(service, buildService)) return solution.IsBuildEnabled;
			if (ReferenceEquals(service, testService)) return solution.IsTestEnabled;
			return true;
		}

		private sealed class DelegateProgress : IProgress<PipelineProgress>
		{
			private readonly Action<PipelineProgress> report;

			public DelegateProgress(Action<PipelineProgress> report)
			{
				this.report = report;
			}

			public void Report(PipelineProgress value) => report(value);
		}

		#endregion

		#region script export

		private bool CanExportScript() =>
			!IsRunning && EnabledServices().Any() && AllSolutions().Any(s => s.IsIncluded);

		private void ExportScript(ScriptExportType exportType)
		{
			bool batch = exportType == ScriptExportType.Batch;
			var dialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = batch ? "Batch File|*.bat" : "PowerShell Script File|*.ps1",
				FileName = batch ? "CheckoutAndBuild.bat" : "CheckoutAndBuild.ps1",
				DefaultExt = batch ? ".bat" : ".ps1"
			};
			if (dialog.ShowDialog() != true)
				return;

			try
			{
				var models = AllSolutions().Select(s => (ISolutionProjectModel)s.Model).ToList();
				ScriptExporter.ExportToFile(EnabledServices(), models, serviceSettings, exportType,
					dialog.FileName, pluginHost.GetExportedValues<IScriptGenerator>());
				LastError = null;
				StatusMessage = "Exported: " + dialog.FileName;
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		#endregion
	}
}
