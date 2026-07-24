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
using CheckoutAndBuild.Core.Services;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Common;
using CheckoutAndBuild.VisualStudio.Settings;

namespace CheckoutAndBuild.VisualStudio.ViewModels
{
	/// <summary>One working folder with the solutions found beneath it.</summary>
	public class WorkingFolderViewModel : NotificationObject
	{
		public WorkingFolderViewModel(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public ObservableCollection<SolutionViewModel> Solutions { get; } = new ObservableCollection<SolutionViewModel>();

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
		private double progressValue;
		private string lastError;
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
		}

		public ObservableCollection<WorkingFolderViewModel> WorkingFolders { get; } = new ObservableCollection<WorkingFolderViewModel>();

		public ICommand RunCommand { get; }
		public ICommand PauseCommand { get; }
		public ICommand ResumeCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand AddFolderCommand { get; }
		public ICommand RemoveFolderCommand { get; }
		public ICommand RefreshCommand { get; }
		public ICommand OpenSettingsCommand { get; }

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
					model.BuildPriority = settings.Get($"BuildPriority:{model.ItemPath}", globalContext, 0);
				}
				return found;
			});

			foreach (var model in models)
			{
				var solution = new SolutionViewModel(model, this, dispatcher);
				solution.PropertyChanged += OnSolutionPropertyChanged;
				folder.Solutions.Add(solution);
			}
			folder.Resort();
		}

		private void DetachSolutions(WorkingFolderViewModel folder)
		{
			foreach (var solution in folder.Solutions)
			{
				solution.PropertyChanged -= OnSolutionPropertyChanged;
				solution.Detach();
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
			}
			else if (e.PropertyName == nameof(SolutionViewModel.BuildPriority))
			{
				settings.Set($"BuildPriority:{solution.ItemPath}", globalContext, solution.BuildPriority);
				WorkingFolders.FirstOrDefault(f => f.Solutions.Contains(solution))?.Resort();
			}
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

		private void Pause()
		{
			cancellation?.Pause();
			IsPaused = true;
			ProgressText = "Paused";
		}

		private void Resume()
		{
			cancellation?.Resume();
			IsPaused = false;
		}

		private async Task RunPipelineAsync()
		{
			if (IsRunning)
				return;
			var models = AllSolutions().Select(s => (ISolutionProjectModel)s.Model).ToList();
			var services = EnabledServices().ToList();
			if (models.Count == 0 || services.Count == 0)
				return;

			LastError = null;
			IsRunning = true;
			IsPaused = false;
			cancellation = new PausableCancellationTokenSource();
			var context = new PipelineContext
			{
				Settings = serviceSettings,
				PreBuildScript = serviceSettings.PreBuildScriptPath,
				PostBuildScript = serviceSettings.PostBuildScriptPath,
				Progress = new DelegateProgress(OnPipelineProgress)
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
			if (IsRunning)
				return;

			LastError = null;
			IsRunning = true;
			IsPaused = false;
			cancellation = new PausableCancellationTokenSource();
			ProgressText = $"{service.OperationName}: {solution.SolutionFileName}";
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
			cancellation?.Dispose();
			cancellation = null;
			IsRunning = false;
			IsPaused = false;
			ProgressValue = 0;
			foreach (var solution in AllSolutions())
				solution.RefreshResult();
		}

		private void OnPipelineProgress(PipelineProgress progress)
		{
			dispatcher.BeginInvoke(new Action(() =>
			{
				ProgressText = $"{progress.OperationName} ({progress.ServiceIndex + 1}/{progress.ServiceCount})";
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
	}
}
