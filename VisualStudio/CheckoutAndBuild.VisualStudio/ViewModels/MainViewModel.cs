using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
	/// <summary>One working folder with the solutions found beneath it.</summary>
	public class WorkingFolderViewModel : NotificationObject
	{
		public WorkingFolderViewModel(string path)
		{
			Path = path;
			IncludedSolutions = new System.Windows.Data.ListCollectionView(Solutions)
			{
				Filter = item => ((SolutionViewModel)item).IsIncluded
			};
		}

		public string Path { get; }

		public ObservableCollection<SolutionViewModel> Solutions { get; } = new ObservableCollection<SolutionViewModel>();

		/// <summary>Filtered live view of <see cref="Solutions"/> for the "Included" area.</summary>
		public ICollectionView IncludedSolutions { get; }

		/// <summary>Sorts in place by build priority, then name.</summary>
		public void Resort()
		{
			var sorted = Solutions.OrderBy(s => s.BuildPriority)
				.ThenBy(s => s.SolutionFileName, StringComparer.OrdinalIgnoreCase).ToList();
			for (int target = 0; target < sorted.Count; target++)
			{
				int current = Solutions.IndexOf(sorted[target]);
				if (current != target)
					Solutions.Move(current, target);
			}
		}
	}

	/// <summary>Root view model of the CheckoutAndBuild tool window.</summary>
	public class MainViewModel : NotificationObject
	{
		private const string workingFoldersKey = "WorkingFolders";
		private const int maxScanDepth = 3;
		private static readonly string[] skippedDirectories = { ".git", ".vs", "bin", "obj", "node_modules", "packages" };

		private readonly Dispatcher dispatcher;
		private readonly ISettingsService settings;
		private readonly SettingsContext globalContext = new SettingsContext();
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

		public MainViewModel() : this(JsonSettingsService.CreateDefault())
		{
		}

		public MainViewModel(ISettingsService settingsService)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			settings = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
			serviceSettings = new ServiceSettingsAdapter(settings, globalContext);

			// default: everything on except Clean + Test (matches the old default roughly)
			cleanEnabled = settings.Get("Services.Clean", globalContext, false);
			checkoutEnabled = settings.Get("Services.Checkout", globalContext, true);
			restoreEnabled = settings.Get("Services.Restore", globalContext, true);
			buildEnabled = settings.Get("Services.Build", globalContext, true);
			testEnabled = settings.Get("Services.Test", globalContext, false);

			RunCommand = new DelegateCommand(async () => await RunPipelineAsync(),
				() => !IsRunning && EnabledServices().Any() && AllSolutions().Any(s => s.IsIncluded));
			PauseCommand = new DelegateCommand(Pause, () => IsRunning && !IsPaused);
			ResumeCommand = new DelegateCommand(Resume, () => IsRunning && IsPaused);
			CancelCommand = new DelegateCommand(() => cancellation?.Cancel(), () => IsRunning);
			AddFolderCommand = new DelegateCommand(async () => await AddFolderAsync(), () => !IsRunning);
			RemoveFolderCommand = new DelegateCommand(p => RemoveFolder(p as WorkingFolderViewModel), p => !IsRunning && p is WorkingFolderViewModel);
			RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), () => !IsRunning);
			OpenSettingsCommand = new DelegateCommand(OpenGlobalSettings);
			ExportBatchCommand = new DelegateCommand(() => ExportScript(ScriptExportType.Batch), CanExportScript);
			ExportPowershellCommand = new DelegateCommand(() => ExportScript(ScriptExportType.Powershell), CanExportScript);
		}

		/// <summary>Error List sink; set by the tool window control (null in tests).</summary>
		internal CoabErrorListProvider ErrorSink { get; set; }

		public ObservableCollection<WorkingFolderViewModel> WorkingFolders { get; } = new ObservableCollection<WorkingFolderViewModel>();

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

		/// <summary>Non-null while the settings "page" is shown instead of the main content.</summary>
		public SettingsViewModel ActiveSettings
		{
			get { return activeSettings; }
			private set { SetProperty(ref activeSettings, value); }
		}

		/// <summary>Opens the global settings editor (gear button, Tools → Options page).</summary>
		public void OpenGlobalSettings()
		{
			ActiveSettings = new SettingsViewModel(settings, "Settings", null, CloseSettings);
		}

		/// <summary>Opens the solution-scoped settings editor (solution context menu).</summary>
		internal void OpenSolutionSettings(SolutionViewModel solution)
		{
			ActiveSettings = new SettingsViewModel(settings, $"Settings — {solution.SolutionFileName}", solution.ItemPath, CloseSettings);
		}

		private void CloseSettings() => ActiveSettings = null;

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
			private set { SetProperty(ref lastError, value); }
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
			set { if (SetProperty(ref cleanEnabled, value)) settings.Set("Services.Clean", globalContext, value); }
		}

		public bool IsCheckoutEnabled
		{
			get { return checkoutEnabled; }
			set { if (SetProperty(ref checkoutEnabled, value)) settings.Set("Services.Checkout", globalContext, value); }
		}

		public bool IsRestoreEnabled
		{
			get { return restoreEnabled; }
			set { if (SetProperty(ref restoreEnabled, value)) settings.Set("Services.Restore", globalContext, value); }
		}

		public bool IsBuildEnabled
		{
			get { return buildEnabled; }
			set { if (SetProperty(ref buildEnabled, value)) settings.Set("Services.Build", globalContext, value); }
		}

		public bool IsTestEnabled
		{
			get { return testEnabled; }
			set { if (SetProperty(ref testEnabled, value)) settings.Set("Services.Test", globalContext, value); }
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
			var folder = new WorkingFolderViewModel(path);
			WorkingFolders.Add(folder);

			var models = await Task.Run(() =>
			{
				var found = ScanForSolutions(path);
				foreach (var model in found)
				{
					model.IsIncluded = settings.Get($"IsIncluded:{model.ItemPath}", globalContext, true);
					model.BuildPriority = GetInitialBuildPriority(model);
				}
				return found;
			});

			foreach (var model in models)
			{
				var solution = new SolutionViewModel(model, this, dispatcher);
				solution.PropertyChanged += OnSolutionPropertyChanged;
				folder.Solutions.Add(solution);
				if (!solution.IsIncluded)
					InsertExcluded(solution);
			}
			folder.Resort();
		}

		/// <summary>Stored priority wins; without one, a plugin IDefaultBuildPriorityManager may supply the default.</summary>
		private int GetInitialBuildPriority(ISolutionProjectModel model)
		{
			int stored = settings.Get($"BuildPriority:{model.ItemPath}", globalContext, int.MinValue);
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

		private void OnSolutionPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var solution = (SolutionViewModel)sender;
			if (e.PropertyName == nameof(SolutionViewModel.IsIncluded))
			{
				settings.Set($"IsIncluded:{solution.ItemPath}", globalContext, solution.IsIncluded);
				if (solution.IsIncluded)
					ExcludedSolutions.Remove(solution);
				else if (!ExcludedSolutions.Contains(solution))
					InsertExcluded(solution);
				WorkingFolders.FirstOrDefault(f => f.Solutions.Contains(solution))?.IncludedSolutions.Refresh();
			}
			else if (e.PropertyName == nameof(SolutionViewModel.BuildPriority))
			{
				settings.Set($"BuildPriority:{solution.ItemPath}", globalContext, solution.BuildPriority);
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
			var models = AllSolutions().Select(s => (ISolutionProjectModel)s.Model).ToList();
			var services = EnabledServices().ToList();
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
				CustomActions = pluginHost.GetExportedValues<ICustomAction>().ToList()
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
