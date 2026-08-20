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
	/// <summary>State of the pipeline run (drives the status bar look).</summary>
	public enum PipelineRunState
	{
		Idle,
		Running,
		Paused,
		Succeeded,
		Failed,
		Cancelled
	}

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
			RunFolderCommand = new DelegateCommand(
				async () => await owner.RunPipelineForAsync(Solutions.Where(s => s.IsIncluded)),
				() => !owner.IsRunning && Solutions.Any(s => s.IsIncluded));
			IncludedSolutions = new System.Windows.Data.ListCollectionView(Solutions)
			{
				Filter = item => ((SolutionViewModel)item).IsIncluded && owner.MatchesFilter((SolutionViewModel)item)
			};
			Repositories.CollectionChanged += (s, e) =>
			{
				RaisePropertyChanged(nameof(ShowRepositoriesInline));
				RaisePropertyChanged(nameof(ShowRepositoriesSummary));
				RaisePropertyChanged(nameof(RepositoriesSummary));
			};
		}

		/// <summary>Runs the pipeline for the included solutions of this folder only (small run button in the header).</summary>
		public System.Windows.Input.ICommand RunFolderCommand { get; }

		public bool ShowRepositoriesInline => Repositories.Count > 0 && Repositories.Count <= 3;

		public bool ShowRepositoriesSummary => Repositories.Count > 3;

		public string RepositoriesSummary => $"{Repositories.Count} repos";

		public string Path { get; }

		public ObservableCollection<SolutionViewModel> Solutions { get; } = new ObservableCollection<SolutionViewModel>();

		/// <summary>Distinct git repositories of the solutions beneath this folder (branch selector in the header).</summary>
		public ObservableCollection<RepositoryBranchViewModel> Repositories { get; } = new ObservableCollection<RepositoryBranchViewModel>();

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
			SyncCommand = new DelegateCommand(async () => await SyncAsync(), () => !IsSyncing);
		}

		public System.Windows.Input.ICommand SyncCommand { get; }

		private bool isSyncing;

		public bool IsSyncing
		{
			get { return isSyncing; }
			private set { SetProperty(ref isSyncing, value); }
		}

		/// <summary>Fetch, pull when behind, push when ahead (sets the upstream when missing).</summary>
		public async Task SyncAsync()
		{
			if (IsSyncing)
				return;
			IsSyncing = true;
			try
			{
				owner.LastError = null;
				await git.FetchAsync(RepositoryPath);
				var status = await git.GetAheadBehindAsync(RepositoryPath);
				if (status.HasUpstream && status.Behind > 0)
				{
					bool stashed = await git.AutoStashAsync(RepositoryPath, owner.AutoStashEnabled);
					try
					{
						await git.PullAsync(RepositoryPath);
					}
					finally
					{
						if (stashed && !await git.TryAutoStashPopAsync(RepositoryPath))
							owner.LastError = $"Auto-stash restore conflicted in {RepositoryName} — your changes remain in stash@{{0}}.";
					}
				}
				status = await git.GetAheadBehindAsync(RepositoryPath);
				if (!status.HasUpstream || status.Ahead > 0)
					await git.PushAsync(RepositoryPath, setUpstream: !status.HasUpstream);
				await LoadCurrentBranchAsync();
			}
			catch (Exception e)
			{
				owner.LastError = e.Message;
			}
			finally
			{
				IsSyncing = false;
			}
		}

		public string RepositoryPath { get; }

		public bool ShowRepositoryName { get; }

		public string RepositoryName => System.IO.Path.GetFileName(RepositoryPath);

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

		private string syncBadge;

		public string SyncBadge
		{
			get { return syncBadge; }
			private set { SetProperty(ref syncBadge, value); }
		}

		/// <summary>Loads <see cref="CurrentBranch"/>; call from the UI thread (continuation updates bindings).</summary>
		public async Task LoadCurrentBranchAsync()
		{
			try
			{
				CurrentBranch = await git.GetCurrentBranchAsync(RepositoryPath);
				var status = await git.GetAheadBehindAsync(RepositoryPath);
				SyncBadge = FormatSyncBadge(status);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild branch load failed: " + e.Message);
			}
		}

		private static string FormatSyncBadge(CheckoutAndBuild.Core.Git.BranchSyncStatus status)
		{
			if (!status.HasUpstream || (status.Ahead == 0 && status.Behind == 0))
				return null;
			return ((status.Ahead > 0 ? $"↑{status.Ahead} " : "") + (status.Behind > 0 ? $"↓{status.Behind}" : "")).Trim();
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
				owner.LastError = null;
				bool stashed = await git.AutoStashAsync(RepositoryPath, owner.AutoStashEnabled);
				try
				{
					await git.CheckoutBranchAsync(RepositoryPath, branch);
				}
				finally
				{
					if (stashed && !await git.TryAutoStashPopAsync(RepositoryPath))
						owner.LastError = $"Auto-stash restore conflicted in {RepositoryName} — your changes remain in stash@{{0}}.";
				}
				CurrentBranch = branch;
				var status = await git.GetAheadBehindAsync(RepositoryPath);
				SyncBadge = FormatSyncBadge(status);
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
		private PipelineRunState runState = PipelineRunState.Idle;
		private DateTime runStartUtc;
		private TimeSpan? finalElapsed;
		private TimeSpan? runEstimate;
		private readonly DispatcherTimer elapsedTimer;
		private readonly DispatcherTimer scheduleTimer;

		private static MainViewModel shared;

		public static MainViewModel Shared => shared ?? (shared = new MainViewModel());

		public MainViewModel() : this(JsonSettingsService.CreateDefault())
		{
		}

		public MainViewModel(ISettingsService settingsService)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			settings = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

			Profiles = new ObservableCollection<string>(
				settings.Get<List<string>>(profilesKey, globalContext) ?? new List<string>());
			if (!Profiles.Contains(SettingsContext.DefaultProfile))
				Profiles.Insert(0, SettingsContext.DefaultProfile);
			currentProfile = settings.Get(currentProfileKey, globalContext, SettingsContext.DefaultProfile);
			if (!Profiles.Contains(currentProfile))
				currentProfile = SettingsContext.DefaultProfile;
			profileContext.Profile = currentProfile;

			serviceSettings = new ServiceSettingsAdapter(settings, profileContext);

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

			elapsedTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
			elapsedTimer.Tick += (s, e) => RaisePropertyChanged(nameof(StatusLineText));
			scheduleTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(60) };
			scheduleTimer.Tick += (s, e) => { CheckScheduledRun(); CheckWatchRun(); };
			scheduleTimer.Start();

			AddSolutionCommand = new DelegateCommand(async p => await AddSolutionAsync(p as WorkingFolderViewModel),
				p => !IsRunning && p is WorkingFolderViewModel);
			RemoveCustomSolutionCommand = new DelegateCommand(p => RemoveCustomSolution(p as SolutionViewModel),
				p => !IsRunning && (p as SolutionViewModel)?.IsCustom == true);
			MergeFolderCommand = new DelegateCommand(p => MergeFolder(p as WorkingFolderViewModel),
				p => !IsRunning && (p as WorkingFolderViewModel)?.Solutions.Count(s => s.IsIncluded) > 1);
			SetSortModeCommand = new DelegateCommand(p => SetSortMode(p));
			RetryFailedCommand = new DelegateCommand(async () => await RunPipelineAsync(onlyFailed: true),
				() => !IsRunning && AllSolutions().Any(s => s.IsIncluded && s.HasFailed));
			SuggestPrioritiesCommand = new DelegateCommand(async () => await SuggestPrioritiesAsync(),
				() => !IsRunning && AllSolutions().Any());
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

		internal CoabErrorListProvider ErrorSink { get; set; }

		internal ISettingsService Settings => settings;

		internal SettingsContext ProfileContext => profileContext;

		private bool UseBranchSpecificSettings =>
			settings.Get("MiscellaneousSettings.UseBranchSpecificSettings", profileContext, false);

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

		public ObservableCollection<string> Profiles { get; }

		public string CurrentProfile
		{
			get { return currentProfile; }
			set
			{
				if (string.IsNullOrEmpty(value) || value == currentProfile)
					return;
				if (IsRunning || !Profiles.Contains(value))
				{
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
		public ICommand RetryFailedCommand { get; }
		public ICommand SuggestPrioritiesCommand { get; }

		private async Task SuggestPrioritiesAsync()
		{
			var solutions = AllSolutions().ToList();
			if (solutions.Count == 0)
				return;
			try
			{
				var models = solutions.Select(s => s.Model).ToList();
				var suggested = await Task.Run(() => CheckoutAndBuild.Core.Analysis.DependencyAnalyzer.SuggestBuildPriorities(models));
				int changed = 0;
				foreach (var solution in solutions)
				{
					if (suggested.TryGetValue(solution.ItemPath, out int priority) && solution.BuildPriority != priority)
					{
						solution.BuildPriority = priority;
						changed++;
					}
				}
				int levels = suggested.Count == 0 ? 0 : suggested.Values.Max() + 1;
				LastError = null;
				StatusMessage = $"Build priorities suggested: {levels} level(s), {changed} solution(s) changed.";
			}
			catch (Exception e)
			{
				LastError = e.Message;
			}
		}

		#region durations + ETA (per service+solution, persisted)

		private static string DurationsKey(string solutionPath) => "Durations:" + solutionPath;

		internal void RecordDuration(SolutionViewModel solution, string operationName, TimeSpan duration)
		{
			if (string.IsNullOrEmpty(operationName) || duration <= TimeSpan.Zero)
				return;
			var durations = settings.Get<Dictionary<string, double>>(DurationsKey(solution.ItemPath), globalContext)
				?? new Dictionary<string, double>();
			durations[operationName] = duration.TotalSeconds;
			settings.Set(DurationsKey(solution.ItemPath), globalContext, durations);
		}

		internal IReadOnlyDictionary<string, double> GetDurations(SolutionViewModel solution) =>
			settings.Get<Dictionary<string, double>>(DurationsKey(solution.ItemPath), globalContext)
			?? new Dictionary<string, double>();

		private TimeSpan? EstimateRun(IReadOnlyCollection<SolutionViewModel> solutions)
		{
			var operationNames = new List<string>();
			foreach (var service in AllServices())
			{
				if (solutions.Any(s => IsServiceEnabledFor(service, s)))
					operationNames.Add(service.OperationName);
			}
			double total = 0;
			foreach (var solution in solutions)
			{
				var durations = GetDurations(solution);
				foreach (string operation in operationNames)
				{
					if (IsServiceEnabledForName(operation, solution) && durations.TryGetValue(MapOperationName(operation), out double seconds))
						total += seconds;
				}
			}
			return total > 1 ? TimeSpan.FromSeconds(total) : (TimeSpan?)null;
		}

		private static string MapOperationName(string serviceOperationName)
		{
			switch (serviceOperationName)
			{
				case "Clean": return "Cleaning";
				case "Checkout": return "Checkout";
				case "Build": return "Building";
				case "Run Unit Tests": return "Run Unit tests";
				default: return serviceOperationName;
			}
		}

		private bool IsServiceEnabledForName(string operationName, SolutionViewModel solution)
		{
			foreach (var service in AllServices())
			{
				if (service.OperationName == operationName)
					return IsServiceEnabledFor(service, solution);
			}
			return false;
		}

		#endregion

		#region scheduled run (morning build)

		private void CheckScheduledRun()
		{
			try
			{
				if (IsRunning || !settings.Get("ScheduledRunEnabled", profileContext, false))
					return;
				string timeText = settings.Get("ScheduledRunTime", profileContext, "08:00");
				if (!TimeSpan.TryParse(timeText, out TimeSpan scheduled))
					return;
				var now = DateTime.Now;
				if (now.TimeOfDay < scheduled || now.TimeOfDay > scheduled + TimeSpan.FromMinutes(5))
					return;
				string today = now.ToString("yyyy-MM-dd");
				if (settings.Get<string>("LastScheduledRun", globalContext) == today)
					return;
				settings.Set("LastScheduledRun", globalContext, today);
				StatusMessage = $"Scheduled run started ({timeText}).";
				_ = RunPipelineAsync();
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild scheduled run failed: " + e.Message);
			}
		}

		#endregion

		#region watch mode

		internal bool AutoStashEnabled => settings.Get("AutoStash", profileContext, true);

		private DateTime lastWatchCheck;
		private bool watchBusy;

		/// <summary>Watch mode: fetches all repositories on the configured interval and starts the pipeline when one is behind.</summary>
		private void CheckWatchRun()
		{
			try
			{
				if (IsRunning || watchBusy || !settings.Get("WatchModeEnabled", profileContext, false))
					return;
				int interval = Math.Max(1, settings.Get("WatchIntervalMinutes", profileContext, 10));
				if ((DateTime.Now - lastWatchCheck).TotalMinutes < interval)
					return;
				lastWatchCheck = DateTime.Now;
				var roots = WorkingFolders.SelectMany(f => f.Repositories)
					.Select(r => r.RepositoryPath)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (roots.Count == 0)
					return;
				watchBusy = true;
				_ = Task.Run(async () =>
				{
					try
					{
						var git = new CheckoutAndBuild.Core.Git.GitService();
						bool anyBehind = false;
						foreach (string root in roots)
						{
							try
							{
								await git.FetchAsync(root);
								var status = await git.GetAheadBehindAsync(root);
								if (status.HasUpstream && status.Behind > 0)
									anyBehind = true;
							}
							catch (Exception e)
							{
								System.Diagnostics.Trace.WriteLine("CheckoutAndBuild watch fetch failed: " + e.Message);
							}
						}
						if (!anyBehind)
							return;
						await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
						if (IsRunning)
							return;
						StatusMessage = "Watch mode: repository behind — starting pipeline.";
						await RunPipelineAsync();
					}
					catch (Exception e)
					{
						System.Diagnostics.Trace.WriteLine("CheckoutAndBuild watch run failed: " + e.Message);
					}
					finally
					{
						watchBusy = false;
					}
				});
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild watch check failed: " + e.Message);
			}
		}

		#endregion

		#region solution multi-selection

		private SolutionViewModel lastClickedSolution;
		private bool hasMultiSelection;

		public bool HasMultiSelection
		{
			get { return hasMultiSelection; }
			private set { SetProperty(ref hasMultiSelection, value); }
		}

		internal IReadOnlyList<SolutionViewModel> SelectedSolutions =>
			AllSolutions().Where(s => s.IsSelected).ToList();

		/// <summary>Row click in the solution list: plain click selects one, Ctrl toggles, Shift selects the range.</summary>
		internal void HandleRowClick(SolutionViewModel solution, System.Windows.Input.ModifierKeys modifiers)
		{
			var ordered = AllSolutions().ToList();
			if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
			{
				solution.IsSelected = !solution.IsSelected;
			}
			else if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) && lastClickedSolution != null)
			{
				int from = ordered.IndexOf(lastClickedSolution);
				int to = ordered.IndexOf(solution);
				if (from < 0 || to < 0)
					return;
				if (from > to)
					(from, to) = (to, from);
				for (int index = 0; index < ordered.Count; index++)
					ordered[index].IsSelected = index >= from && index <= to;
			}
			else
			{
				bool wasOnlySelection = solution.IsSelected && ordered.Count(s => s.IsSelected) == 1;
				foreach (var other in ordered)
					other.IsSelected = false;
				solution.IsSelected = !wasOnlySelection;
			}
			if (!modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
				lastClickedSolution = solution;
			HasMultiSelection = ordered.Count(s => s.IsSelected) > 1;
		}

		internal Task RunSelectionAsync() => RunPipelineForAsync(SelectedSolutions.Where(s => s.IsIncluded));

		internal Task RunServiceForSelectionAsync(IOperationService service) =>
			RunServiceForAsync(SelectedSolutions, service);

		#endregion

		#region filter + sort

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

		internal void OpenSolutionSettings(SolutionViewModel solution)
		{
			ActiveSettings = new SettingsViewModel(settings, $"Settings — {solution.SolutionFileName}", solution.ItemPath, CloseSettings,
				SettingsUiFactory.GetSettingsClasses(pluginHost), CurrentProfile);
		}

		private void CloseSettings()
		{
			ActiveSettings = null;
			ReloadProfileScopedState();
		}

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

		#region status bar state (colored bar with glyph + elapsed time)

		public PipelineRunState RunState
		{
			get { return runState; }
			private set
			{
				if (SetProperty(ref runState, value))
				{
					RaisePropertyChanged(nameof(StateBrush));
					RaisePropertyChanged(nameof(StatusGlyph));
					RaisePropertyChanged(nameof(StatusLineText));
					UpdateTaskbar();
				}
			}
		}

		private static readonly System.Windows.Media.Brush RunningBrush = FrozenBrush(0x00, 0x78, 0xD7);
		private static readonly System.Windows.Media.Brush PausedBrush = FrozenBrush(0xE8, 0x8C, 0x00);
		private static readonly System.Windows.Media.Brush SucceededBrush = FrozenBrush(0x38, 0x8A, 0x34);
		private static readonly System.Windows.Media.Brush FailedBrush = FrozenBrush(0xB2, 0x22, 0x22);
		private static readonly System.Windows.Media.Brush CancelledBrush = FrozenBrush(0x80, 0x80, 0x80);

		private static System.Windows.Media.Brush FrozenBrush(byte r, byte g, byte b)
		{
			var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
			brush.Freeze();
			return brush;
		}

		public System.Windows.Media.Brush StateBrush
		{
			get
			{
				switch (runState)
				{
					case PipelineRunState.Running: return RunningBrush;
					case PipelineRunState.Paused: return PausedBrush;
					case PipelineRunState.Succeeded: return SucceededBrush;
					case PipelineRunState.Failed: return FailedBrush;
					case PipelineRunState.Cancelled: return CancelledBrush;
					default: return System.Windows.Media.Brushes.Transparent;
				}
			}
		}

		public string StatusGlyph
		{
			get
			{
				switch (runState)
				{
					case PipelineRunState.Succeeded: return ""; // check
					case PipelineRunState.Failed: return "";    // error badge
					case PipelineRunState.Cancelled: return ""; // cancel
					case PipelineRunState.Paused: return "";    // pause
					default: return "";
				}
			}
		}

		private TimeSpan RunElapsed => finalElapsed
			?? (runState == PipelineRunState.Running || runState == PipelineRunState.Paused
				? DateTime.UtcNow - runStartUtc
				: TimeSpan.Zero);

		private static string FormatElapsed(TimeSpan elapsed) =>
			elapsed.TotalHours >= 1 ? elapsed.ToString("h\\:mm\\:ss") : elapsed.ToString("mm\\:ss");

		public string StatusLineText
		{
			get
			{
				switch (runState)
				{
					case PipelineRunState.Running:
						string eta = runEstimate.HasValue && runEstimate.Value > RunElapsed
							? $"  •  ~{FormatElapsed(runEstimate.Value - RunElapsed)} left"
							: "";
						return string.IsNullOrEmpty(lastProgressText)
							? $"Running…  {FormatElapsed(RunElapsed)}{eta}"
							: $"{lastProgressText}   {FormatElapsed(RunElapsed)}{eta}";
					case PipelineRunState.Paused:
						return $"Paused   {FormatElapsed(RunElapsed)}";
					case PipelineRunState.Succeeded:
						return $"Done in {FormatElapsed(RunElapsed)}";
					case PipelineRunState.Failed:
						int failed = AllSolutions().Count(s => s.HasFailed);
						return failed > 0
							? $"Finished in {FormatElapsed(RunElapsed)} — {failed} solution(s) failed"
							: $"Failed after {FormatElapsed(RunElapsed)}";
					case PipelineRunState.Cancelled:
						return $"Cancelled after {FormatElapsed(RunElapsed)}";
					default:
						return "Ready";
				}
			}
		}

		private void BeginRunStatus()
		{
			runStartUtc = DateTime.UtcNow;
			finalElapsed = null;
			ProgressValue = 0;
			RunState = PipelineRunState.Running;
			elapsedTimer.Start();
		}

		private void EndRunStatus(bool cancelled, bool crashed)
		{
			elapsedTimer.Stop();
			finalElapsed = DateTime.UtcNow - runStartUtc;
			ProgressValue = 100;
			if (cancelled)
				RunState = PipelineRunState.Cancelled;
			else if (crashed || AllSolutions().Any(s => s.HasFailed))
				RunState = PipelineRunState.Failed;
			else
				RunState = PipelineRunState.Succeeded;
			ShowToastIfBackground();
		}

		private void ShowToastIfBackground()
		{
			try
			{
				if (Application.Current?.Windows.OfType<Window>().Any(w => w.IsActive) == true)
					return;
				var icon = new System.Windows.Forms.NotifyIcon
				{
					Icon = System.Drawing.SystemIcons.Information,
					Visible = true
				};
				icon.BalloonTipClosed += (s, e) => { icon.Visible = false; icon.Dispose(); };
				icon.BalloonTipClicked += (s, e) => { icon.Visible = false; icon.Dispose(); };
				icon.ShowBalloonTip(5000, "CheckoutAndBuild", StatusLineText,
					RunState == PipelineRunState.Succeeded
						? System.Windows.Forms.ToolTipIcon.Info
						: System.Windows.Forms.ToolTipIcon.Error);
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild toast failed: " + e.Message);
			}
		}

		private void UpdateTaskbar()
		{
			try
			{
				var mainWindow = Application.Current?.MainWindow;
				if (mainWindow == null)
					return;
				var info = mainWindow.TaskbarItemInfo ?? (mainWindow.TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo());
				switch (runState)
				{
					case PipelineRunState.Running:
						info.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
						info.ProgressValue = Math.Max(0.02, ProgressValue / 100.0);
						break;
					case PipelineRunState.Paused:
						info.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Paused;
						break;
					case PipelineRunState.Failed:
						info.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
						info.ProgressValue = 1;
						break;
					default:
						info.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
						break;
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Trace.WriteLine("CheckoutAndBuild taskbar progress failed: " + e.Message);
			}
		}

		#endregion

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

			CurrentProfile = SettingsContext.DefaultProfile;
			Profiles.Remove(name);
			PersistProfiles();
		}

		private void PersistProfiles()
		{
			settings.Set(profilesKey, globalContext, Profiles.ToList());
		}

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
				Description = "Select a working folder to scan for solutions (*.sln, *.slnx)"
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
					}
				}
				foreach (var model in found)
				{
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

		internal void AddFolderByPath(string path)
		{
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path)
				|| WorkingFolders.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
				return;
			_ = dispatcher.BeginInvoke(new Action(async () =>
			{
				try
				{
					await AddFolderCoreAsync(path);
					PersistFolders();
				}
				catch (Exception e)
				{
					LastError = e.Message;
				}
			}));
		}

		private async Task AddSolutionAsync(WorkingFolderViewModel folder)
		{
			if (folder == null)
				return;
			var dialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Solution files|*.sln;*.slnx",
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
					foreach (string sln in Directory.EnumerateFiles(directory, "*.sln")
						.Concat(Directory.EnumerateFiles(directory, "*.slnx")))
					{
						try
						{
							result.Add(SolutionParser.Parse(sln));
						}
						catch (Exception)
						{
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

		private void OnPausedChanged(object sender, bool paused)
		{
			if (!dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke(new Action(() => OnPausedChanged(sender, paused)));
				return;
			}
			IsPaused = paused;
			ProgressText = paused ? "Paused" : lastProgressText;
			if (runState == PipelineRunState.Running || runState == PipelineRunState.Paused)
				RunState = paused ? PipelineRunState.Paused : PipelineRunState.Running;
			foreach (var solution in AllSolutions())
				solution.RefreshResult();
		}

		internal Task RunPipelineForAsync(IEnumerable<SolutionViewModel> subset) =>
			RunPipelineAsync(onlyFailed: false, subset: subset.ToList());

		private async Task RunPipelineAsync(bool onlyFailed = false, IReadOnlyCollection<SolutionViewModel> subset = null)
		{
			await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			if (IsRunning)
				return;
			var solutions = (subset ?? AllSolutions()).Where(s => !onlyFailed || s.HasFailed).ToList();
			var models = solutions.Select(s => (ISolutionProjectModel)s.Model).ToList();

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
			BeginRunStatus();
			runEstimate = EstimateRun(solutions.Where(s => s.IsIncluded).ToList());

			var gitForSkip = new CheckoutAndBuild.Core.Git.GitService();
			Dictionary<string, string> revisionsBeforePull = null;
			var unchangedRepos = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
			string RepoRootOf(ISolutionProjectModel m) => (m as CheckoutAndBuild.Core.Model.SolutionProjectModel)?.GitRepositoryRoot;
			if (settings.Get("SkipUnchanged", profileContext, false) && enabledModels[checkoutService].Count > 0)
			{
				var repoRoots = models.Select(RepoRootOf)
					.Where(r => !string.IsNullOrEmpty(r))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				revisionsBeforePull = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (string root in repoRoots)
					revisionsBeforePull[root] = await gitForSkip.GetRevisionAsync(root);
			}

			bool IsUnchangedRepository(ISolutionProjectModel model)
			{
				string root = RepoRootOf(model);
				if (root == null || revisionsBeforePull == null || !revisionsBeforePull.TryGetValue(root, out string before) || before == null)
					return false;
				return unchangedRepos.GetOrAdd(root,
					r => gitForSkip.GetRevisionAsync(r).GetAwaiter().GetResult() == before);
			}

			var context = new PipelineContext
			{
				Settings = serviceSettings,
				PreBuildScript = serviceSettings.PreBuildScriptPath,
				PostBuildScript = serviceSettings.PostBuildScriptPath,
				Progress = new DelegateProgress(OnPipelineProgress),
				CustomActions = pluginHost.GetExportedValues<ICustomAction>().ToList(),
				ServiceProjectFilter = (service, model) =>
				{
					if (enabledModels.TryGetValue(service, out var enabled) && !enabled.Contains(model))
						return false;
					if (revisionsBeforePull != null
						&& !ReferenceEquals(service, cleanService)
						&& !ReferenceEquals(service, checkoutService)
						&& IsUnchangedRepository(model))
						return false;
					return true;
				}
			};

			try
			{
				await Task.Run(() => pipelineRunner.RunAsync(models, services, context, cancellation));
				EndRunStatus(cancelled: cancellation.IsCancellationRequested, crashed: false);
				if (revisionsBeforePull != null)
				{
					int skipped = models.Count(m => RepoRootOf(m) != null
						&& unchangedRepos.TryGetValue(RepoRootOf(m), out bool unchanged) && unchanged);
					if (skipped > 0)
						StatusMessage = $"{skipped} solution(s) skipped — repository unchanged.";
				}
			}
			catch (OperationCanceledException)
			{
				EndRunStatus(cancelled: true, crashed: false);
			}
			catch (Exception e)
			{
				LastError = e.Message;
				EndRunStatus(cancelled: false, crashed: true);
			}
			finally
			{
				FinishRun();
			}
		}

		internal Task RunSingleServiceAsync(SolutionViewModel solution, IOperationService service) =>
			RunServiceForAsync(new[] { solution }, service);

		internal async Task RunServiceForAsync(IReadOnlyList<SolutionViewModel> solutions, IOperationService service)
		{
			await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			if (IsRunning || solutions.Count == 0)
				return;

			LastError = null;
			StatusMessage = null;
			ErrorSink?.Clear();
			IsRunning = true;
			IsPaused = false;
			cancellation = new PausableCancellationTokenSource();
			cancellation.PausedChanged += OnPausedChanged;
			ProgressText = lastProgressText = solutions.Count == 1
				? $"{service.OperationName}: {solutions[0].SolutionFileName}"
				: $"{service.OperationName}: {solutions.Count} solutions";
			ProgressValue = 0;
			BeginRunStatus();

			try
			{
				var models = solutions.Select(s => (ISolutionProjectModel)s.Model).ToList();
				await Task.Run(() => service.ExecuteAsync(models, serviceSettings, cancellation));
				EndRunStatus(cancelled: cancellation.IsCancellationRequested, crashed: false);
			}
			catch (OperationCanceledException)
			{
				EndRunStatus(cancelled: true, crashed: false);
			}
			catch (Exception e)
			{
				LastError = e.Message;
				EndRunStatus(cancelled: false, crashed: true);
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
			foreach (var solution in AllSolutions())
				solution.RefreshResult();
			ReportResultsToErrorList();
		}

		private void ReportResultsToErrorList()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			if (ErrorSink == null)
				return;
			try
			{
				foreach (var solution in AllSolutions())
				{
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
				RaisePropertyChanged(nameof(StatusLineText));
				if (progress.ServiceCount > 0)
					ProgressValue = (progress.ServiceIndex + 1) * 100.0 / progress.ServiceCount;
				UpdateTaskbar();
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
