using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Services;
using CheckoutAndBuild.VisualStudio.Common;

namespace CheckoutAndBuild.VisualStudio.ViewModels
{
	/// <summary>
	/// Wraps a <see cref="SolutionProjectModel"/> for the tool window. The model raises
	/// PropertyChanged on background threads during pipeline runs; this view model marshals
	/// all notifications onto the UI dispatcher.
	/// </summary>
	public class SolutionViewModel : NotificationObject
	{
		private readonly MainViewModel owner;
		private readonly Dispatcher dispatcher;
		private OperationInfo observedOperation;
		private Dictionary<string, bool> serviceOverrides;
		private DateTime operationStartUtc;
		private string runningOperationName;

		public SolutionViewModel(SolutionProjectModel model, MainViewModel owner, Dispatcher dispatcher)
		{
			Model = model ?? throw new ArgumentNullException(nameof(model));
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

			serviceOverrides = owner.Settings.Get<Dictionary<string, bool>>(ServicesKey, owner.ContextFor(model))
				?? new Dictionary<string, bool>();
			ApplyBuildOptionsToModel();

			model.PropertyChanged += OnModelPropertyChanged;
			ObserveOperation(model.CurrentOperation);

			BuildOnlyCommand = new DelegateCommand(async () => await owner.RunSingleServiceAsync(this, owner.BuildOperation), () => !owner.IsRunning);
			CleanOnlyCommand = new DelegateCommand(async () => await owner.RunSingleServiceAsync(this, owner.CleanOperation), () => !owner.IsRunning);
			TestOnlyCommand = new DelegateCommand(async () => await owner.RunSingleServiceAsync(this, owner.TestOperation), () => !owner.IsRunning);
			IncreasePriorityCommand = new DelegateCommand(() => BuildPriority = Math.Max(0, BuildPriority - 1), () => !owner.IsRunning && BuildPriority > 0);
			DecreasePriorityCommand = new DelegateCommand(() => BuildPriority = BuildPriority + 1, () => !owner.IsRunning);
			SettingsCommand = new DelegateCommand(() => owner.OpenSolutionSettings(this));
			EditBuildPropertiesCommand = new DelegateCommand(EditBuildProperties);
			EditBuildTargetsCommand = new DelegateCommand(EditBuildTargets);

			OpenSolutionCommand = new DelegateCommand(OpenSolution);
			RemoveFromListCommand = new DelegateCommand(() => owner.RemoveCustomSolution(this), () => IsCustom && !owner.IsRunning);
			StartCommand = new DelegateCommand(() => StartExecutable(attachDebugger: false), () => FindExecutable() != null);
			StartDebuggerCommand = new DelegateCommand(() => StartExecutable(attachDebugger: true), () => FindExecutable() != null);
			StopCommand = new DelegateCommand(StopExecutable, () => FindExecutable() != null);
			RestartCommand = new DelegateCommand(() => { StopExecutable(); StartExecutable(attachDebugger: false); }, () => FindExecutable() != null);
			ShowErrorsCommand = new DelegateCommand(ShowErrors, () => HasFailed);
			OpenInExplorerCommand = new DelegateCommand(
				() => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ItemPath}\""));
			OpenOutputDirectoryCommand = new DelegateCommand(
				() => System.Diagnostics.Process.Start("explorer.exe", $"\"{FirstExistingOutputPath()}\""),
				() => FirstExistingOutputPath() != null);
			ShowHistoryCommand = new DelegateCommand(
				() => CheckoutAndBuildPackage.Instance?.ShowGitHistory(Model.GitRepositoryRoot),
				() => Model.GitRepositoryRoot != null);
			OpenInGitWindowCommand = new DelegateCommand(
				() => CheckoutAndBuildPackage.Instance?.ShowGitRepository(Model.GitRepositoryRoot),
				() => Model.GitRepositoryRoot != null);
			CopyFullPathCommand = new DelegateCommand(() => Clipboard.SetText(ItemPath));
		}

		private void OpenSolution()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			var solution = Microsoft.VisualStudio.Shell.Package.GetGlobalService(
				typeof(Microsoft.VisualStudio.Shell.Interop.SVsSolution)) as Microsoft.VisualStudio.Shell.Interop.IVsSolution;
			solution?.OpenSolutionFile(0, ItemPath);
		}

		private string FirstExistingOutputPath()
		{
			return Model.Projects
				.Select(p => p.OutputPath)
				.FirstOrDefault(p => !string.IsNullOrEmpty(p) && Directory.Exists(p));
		}

		public bool IsCustom { get; set; }

		public Microsoft.VisualStudio.Imaging.Interop.ImageMoniker IconMoniker =>
			Model.IsDelphiProject
				? Microsoft.VisualStudio.Imaging.KnownMonikers.ApplicationGroup
				: Microsoft.VisualStudio.Imaging.KnownMonikers.Solution;

		#region start/stop of the built executable (old Start/Stop/Restart commands)

		private string executablePath;
		private bool executableSearched;

		internal string FindExecutable()
		{
			if (executableSearched)
				return executablePath;
			executableSearched = true;
			executablePath = Model.Projects
				.Where(p => !string.IsNullOrEmpty(p.OutputPath))
				.Select(p => Path.Combine(p.OutputPath, (p.AssemblyName ?? p.Name) + ".exe"))
				.FirstOrDefault(File.Exists);
			return executablePath;
		}

		private void StartExecutable(bool attachDebugger)
		{
			string exe = FindExecutable();
			if (exe == null)
				return;
			try
			{
				var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
				{
					WorkingDirectory = Path.GetDirectoryName(exe)
				});
				if (attachDebugger && process != null)
					AttachDebugger(process.Id);
				owner.LastError = null;
			}
			catch (Exception e)
			{
				owner.LastError = e.Message;
			}
		}

		private static void AttachDebugger(int processId)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
			if (dte == null)
				return;
			foreach (EnvDTE.Process process in dte.Debugger.LocalProcesses)
			{
				if (process.ProcessID == processId)
				{
					process.Attach();
					return;
				}
			}
		}

		private void StopExecutable()
		{
			try
			{
				var outputDirs = Model.Projects
					.Select(p => p.OutputPath)
					.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p));
				CheckoutAndBuild.Core.Execution.RunningProcessHelper.KillProcessesInDirectories(outputDirs);
			}
			catch (Exception e)
			{
				owner.LastError = e.Message;
			}
		}

		private void OpenWith(VsInstance instance)
		{
			try
			{
				string devenv = instance?.ProductPath
					?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
				if (devenv != null)
					System.Diagnostics.Process.Start(devenv, $"\"{ItemPath}\"");
			}
			catch (Exception e)
			{
				owner.LastError = e.Message;
			}
		}

		#endregion

		#region error dialog with retry (old BuildErrorsViewModel)

		private sealed class ErrorRow
		{
			public string Location { get; set; }
			public string Message { get; set; }
			public string File { get; set; }
			public int Line { get; set; }
		}

		private void ShowErrors()
		{
			object result = Model.Result ?? Model.ErrorContent;
			var rows = new List<ErrorRow>();
			IOperationService retryService = null;
			switch (result)
			{
				case BuildResult build:
					rows.AddRange(build.Errors.Select(e => new ErrorRow
					{
						Location = $"{Path.GetFileName(e.File)}({e.Line})",
						Message = $"{(e.IsWarning ? "warning" : "error")} {e.Code}: {e.Message}",
						File = e.File,
						Line = e.Line
					}));
					retryService = owner.BuildOperation;
					break;
				case TestRunResult tests:
					rows.AddRange(tests.Failures.Select(f => new ErrorRow
					{
						Location = f.TestName,
						Message = f.Message,
						File = null
					}));
					retryService = owner.TestOperation;
					break;
				case Exception exception:
					rows.Add(new ErrorRow { Location = SolutionFileName, Message = exception.Message });
					break;
				default:
					return;
			}

			var list = new ListView { ItemsSource = rows, Margin = new Thickness(8) };
			var view = new GridView();
			view.Columns.Add(new GridViewColumn { Header = "Where", Width = 220, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ErrorRow.Location)) });
			view.Columns.Add(new GridViewColumn { Header = "Message", Width = 420, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(ErrorRow.Message)) });
			list.View = view;
			list.MouseDoubleClick += (s, e) =>
			{
				if (list.SelectedItem is ErrorRow row && row.File != null && File.Exists(row.File))
					OpenFileAtLine(row.File, row.Line);
			};

			var retry = new Button { Content = "Retry Operation", Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 8, 8), HorizontalAlignment = HorizontalAlignment.Right };
			var panel = new DockPanel();
			DockPanel.SetDock(retry, Dock.Bottom);
			if (retryService != null)
				panel.Children.Add(retry);
			panel.Children.Add(list);

			var window = CreateOptionsWindow($"Errors — {SolutionFileName}", panel, 380);
			window.Width = 700;
			retry.Click += async (s, e) =>
			{
				window.Close();
				await owner.RunSingleServiceAsync(this, retryService);
			};
			window.ShowDialog();
		}

		private static void OpenFileAtLine(string file, int line)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			try
			{
				Microsoft.VisualStudio.Shell.VsShellUtilities.OpenDocument(
					Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, file,
					Guid.Empty, out _, out _, out var frame, out var textView);
				frame?.Show();
				textView?.SetCaretPos(Math.Max(0, line - 1), 0);
				textView?.CenterLines(Math.Max(0, line - 1), 1);
			}
			catch (Exception)
			{
			}
		}

		#endregion

		public SolutionProjectModel Model { get; }

		public string SolutionFileName => Model.SolutionFileName;

		public string ItemPath => Model.ItemPath;

		public bool IsIncluded
		{
			get { return Model.IsIncluded; }
			set { Model.IsIncluded = value; }
		}

		public int BuildPriority
		{
			get { return Model.BuildPriority; }
			set { Model.BuildPriority = value; }
		}

		#region per-solution service selection (override of the global step checkboxes)

		private string ServicesKey => "Services:" + ItemPath;
		private string BuildPropertiesKey => "BuildProperties:" + ItemPath;
		private string BuildTargetsKey => "BuildTargets:" + ItemPath;

		public bool IsCleanEnabled
		{
			get { return GetServiceEnabled("Clean", owner.IsCleanEnabled); }
			set { SetServiceEnabled("Clean", value); }
		}

		public bool IsCheckoutEnabled
		{
			get { return GetServiceEnabled("Checkout", owner.IsCheckoutEnabled); }
			set { SetServiceEnabled("Checkout", value); }
		}

		public bool IsRestoreEnabled
		{
			get { return GetServiceEnabled("Restore", owner.IsRestoreEnabled); }
			set { SetServiceEnabled("Restore", value); }
		}

		public bool IsBuildEnabled
		{
			get { return GetServiceEnabled("Build", owner.IsBuildEnabled); }
			set { SetServiceEnabled("Build", value); }
		}

		public bool IsTestEnabled
		{
			get { return GetServiceEnabled("Test", owner.IsTestEnabled); }
			set { SetServiceEnabled("Test", value); }
		}

		public bool HasAnyServiceEnabled =>
			IsCleanEnabled || IsCheckoutEnabled || IsRestoreEnabled || IsBuildEnabled || IsTestEnabled;

		public string ServicesCaption
		{
			get
			{
				var names = new List<string>();
				if (IsCleanEnabled) names.Add("Clean");
				if (IsCheckoutEnabled) names.Add("Checkout");
				if (IsRestoreEnabled) names.Add("Nuget Restore");
				if (IsBuildEnabled) names.Add("Build");
				if (IsTestEnabled) names.Add("Run Unit Tests");
				return names.Count == 0 ? "(None)" : string.Join(",", names);
			}
		}

		public string ServicesCaptionSmall
		{
			get
			{
				string caption = ServicesCaption;
				return caption.Length > 40 ? caption.Substring(0, 40) + "..." : caption;
			}
		}

		public string BuildPropertiesCaption => $"Build Properties ({Model.BuildProperties.Count})...";

		public string BuildTargetsCaption => $"Build Targets ({Model.BuildTargets.Count()})...";

		private bool GetServiceEnabled(string key, bool globalValue)
		{
			bool overridden;
			return serviceOverrides.TryGetValue(key, out overridden) ? overridden : globalValue;
		}

		private void SetServiceEnabled(string key, bool value)
		{
			serviceOverrides[key] = value;
			owner.Settings.Set(ServicesKey, owner.ContextFor(Model), serviceOverrides);
			RefreshServiceFlags();
		}

		internal void RefreshServiceFlags()
		{
			RaisePropertyChanged(nameof(IsCleanEnabled));
			RaisePropertyChanged(nameof(IsCheckoutEnabled));
			RaisePropertyChanged(nameof(IsRestoreEnabled));
			RaisePropertyChanged(nameof(IsBuildEnabled));
			RaisePropertyChanged(nameof(IsTestEnabled));
			RaisePropertyChanged(nameof(HasAnyServiceEnabled));
			RaisePropertyChanged(nameof(ServicesCaption));
			RaisePropertyChanged(nameof(ServicesCaptionSmall));
		}

		internal void ReloadProfileScopedState()
		{
			serviceOverrides = owner.Settings.Get<Dictionary<string, bool>>(ServicesKey, owner.ContextFor(Model))
				?? new Dictionary<string, bool>();
			ApplyBuildOptionsToModel();
			RefreshServiceFlags();
			RaisePropertyChanged(nameof(BuildPropertiesCaption));
			RaisePropertyChanged(nameof(BuildTargetsCaption));
		}

		private void ApplyBuildOptionsToModel()
		{
			var context = owner.ContextFor(Model);
			var properties = owner.Settings.Get<Dictionary<string, string>>(BuildPropertiesKey, context);
			Model.BuildProperties.Clear();
			foreach (var pair in properties ?? new Dictionary<string, string>())
				Model.BuildProperties[pair.Key] = pair.Value;
			Model.SetBuildTargets(owner.Settings.Get<List<string>>(BuildTargetsKey, context));
		}

		private void EditBuildProperties()
		{
			var rows = new ObservableCollection<BuildPropertyRow>(
				Model.BuildProperties.Select(p => new BuildPropertyRow { Key = p.Key, Value = p.Value }));
			var grid = new DataGrid
			{
				ItemsSource = rows,
				AutoGenerateColumns = false,
				CanUserAddRows = true,
				CanUserDeleteRows = true,
				HeadersVisibility = DataGridHeadersVisibility.Column,
				Margin = new Thickness(8)
			};
			grid.Columns.Add(new DataGridTextColumn
			{
				Header = "Property",
				Binding = new System.Windows.Data.Binding(nameof(BuildPropertyRow.Key)),
				Width = new DataGridLength(1, DataGridLengthUnitType.Star)
			});
			grid.Columns.Add(new DataGridTextColumn
			{
				Header = "Value",
				Binding = new System.Windows.Data.Binding(nameof(BuildPropertyRow.Value)),
				Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
			});

			var window = CreateOptionsWindow($"Additional Build Properties for {SolutionFileName}", grid, 320);
			window.Closing += (s, e) => grid.CommitEdit(DataGridEditingUnit.Row, true);
			window.ShowDialog();

			var result = new Dictionary<string, string>();
			foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.Key)))
				result[row.Key.Trim()] = row.Value ?? string.Empty;
			owner.Settings.Set(BuildPropertiesKey, owner.ContextFor(Model), result);
			ApplyBuildOptionsToModel();
			RaisePropertyChanged(nameof(BuildPropertiesCaption));
		}

		private void EditBuildTargets()
		{
			var textBox = new TextBox { Text = string.Join(", ", Model.BuildTargets), Margin = new Thickness(8) };
			var hint = new TextBlock
			{
				Text = "Build targets, comma separated (empty = default \"Build\"):",
				Margin = new Thickness(8, 8, 8, 0),
				Opacity = 0.7,
				TextWrapping = TextWrapping.Wrap
			};
			var panel = new StackPanel();
			panel.Children.Add(hint);
			panel.Children.Add(textBox);

			var window = CreateOptionsWindow($"Specific Build Targets for {SolutionFileName}", panel, 150);
			window.ShowDialog();

			var targets = textBox.Text
				.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(t => t.Trim())
				.Where(t => t.Length > 0)
				.ToList();
			owner.Settings.Set(BuildTargetsKey, owner.ContextFor(Model), targets);
			ApplyBuildOptionsToModel();
			RaisePropertyChanged(nameof(BuildTargetsCaption));
		}

		private static Window CreateOptionsWindow(string title, object content, double height)
		{
			return new Window
			{
				Title = title,
				Content = content,
				Width = 420,
				Height = height,
				Owner = Application.Current?.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				WindowStyle = WindowStyle.ToolWindow,
				ShowInTaskbar = false
			};
		}

		#endregion

		public bool IsBusy => Model.IsBusy;

		public string StatusText => owner.IsPaused && IsBusy ? "Paused" : Model.CurrentOperation?.StatusText ?? string.Empty;

		public double Progress => Model.CurrentOperation?.Progress ?? 0;

		public bool IsIndeterminate => Model.CurrentOperation?.IsIndeterminate ?? false;

		public Brush StatusBrush
		{
			get
			{
				if (owner.IsPaused && IsBusy)
					return Brushes.Orange;
				var operation = Model.CurrentOperation;
				if (operation != null)
					return BrushFromName(operation.ColorName);
				object result = Model.Result ?? Model.ErrorContent;
				if (result is Exception
					|| (result is BuildResult build && !build.Success)
					|| (result is TestRunResult tests && !tests.Success))
					return Brushes.Firebrick;
				return result != null ? Brushes.Green : Brushes.Gray;
			}
		}

		public bool HasFailed
		{
			get
			{
				object result = Model.Result ?? Model.ErrorContent;
				return result is Exception
					|| (result is BuildResult build && !build.Success)
					|| (result is TestRunResult tests && !tests.Success);
			}
		}

		public bool HasSucceeded => !HasFailed && (Model.Result ?? Model.ErrorContent) != null;

		public string ResultText
		{
			get
			{
				object result = Model.Result ?? Model.ErrorContent;
				switch (result)
				{
					case BuildResult build:
						return build.Success
							? "Build succeeded"
							: $"Build failed ({build.Errors.Count(e => !e.IsWarning)} error(s))";
					case TestRunResult tests:
						return tests.Failed > 0
							? $"Tests: {tests.Passed}/{tests.Total} passed, {tests.Failed} failed"
							: $"Tests: {tests.Passed}/{tests.Total} passed";
					case Exception exception:
						return FirstLine(exception.Message);
					case null:
						return string.Empty;
					default:
						return result.ToString();
				}
			}
		}

		public ICommand EditBuildPropertiesCommand { get; }
		public ICommand EditBuildTargetsCommand { get; }
		public ICommand BuildOnlyCommand { get; }
		public ICommand CleanOnlyCommand { get; }
		public ICommand TestOnlyCommand { get; }
		public ICommand IncreasePriorityCommand { get; }
		public ICommand SettingsCommand { get; }
		public ICommand DecreasePriorityCommand { get; }
		public ICommand OpenSolutionCommand { get; }
		public ICommand RemoveFromListCommand { get; }

		public IEnumerable<OpenWithOption> OpenWithOptions
		{
			get
			{
				yield return new OpenWithOption("New Visual Studio instance", new DelegateCommand(() => OpenWith(null)));
				foreach (var instance in owner.VsInstances)
					yield return new OpenWithOption(instance.DisplayName, new DelegateCommand(() => OpenWith(instance)));
			}
		}
		public ICommand StartCommand { get; }
		public ICommand StartDebuggerCommand { get; }
		public ICommand StopCommand { get; }
		public ICommand RestartCommand { get; }
		public ICommand ShowErrorsCommand { get; }
		public ICommand OpenInExplorerCommand { get; }
		public ICommand OpenOutputDirectoryCommand { get; }
		public ICommand ShowHistoryCommand { get; }
		public ICommand OpenInGitWindowCommand { get; }
		public ICommand CopyFullPathCommand { get; }

		/// <summary>Re-raises the result/status properties (model does not notify on SetResult).</summary>
		public void RefreshResult() => OnUI(() =>
		{
			executableSearched = false;
			RaiseStatus();
		});

		public void Detach() => Model.PropertyChanged -= OnModelPropertyChanged;

		private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			OnUI(() =>
			{
				switch (e.PropertyName)
				{
					case nameof(SolutionProjectModel.CurrentOperation):
						TrackOperationDuration(Model.CurrentOperation);
						ObserveOperation(Model.CurrentOperation);
						RaiseStatus();
						break;
					case nameof(SolutionProjectModel.IsBusy):
						RaisePropertyChanged(nameof(IsBusy));
						break;
					case nameof(SolutionProjectModel.IsIncluded):
						RaisePropertyChanged(nameof(IsIncluded));
						break;
					case nameof(SolutionProjectModel.BuildPriority):
						RaisePropertyChanged(nameof(BuildPriority));
						break;
					case nameof(SolutionProjectModel.ErrorContent):
						RaisePropertyChanged(nameof(StatusBrush));
						RaisePropertyChanged(nameof(ResultText));
						RaisePropertyChanged(nameof(HasFailed));
						RaisePropertyChanged(nameof(HasSucceeded));
						break;
				}
			});
		}

		private void TrackOperationDuration(OperationInfo newOperation)
		{
			if (runningOperationName != null && newOperation?.StatusText != runningOperationName)
			{
				owner.RecordDuration(this, runningOperationName, DateTime.UtcNow - operationStartUtc);
				runningOperationName = null;
			}
			if (newOperation != null && runningOperationName == null)
			{
				runningOperationName = newOperation.StatusText;
				operationStartUtc = DateTime.UtcNow;
			}
		}

		public string RowToolTip
		{
			get
			{
				var durations = owner.GetDurations(this);
				if (durations.Count == 0)
					return ItemPath;
				var lines = durations.Select(p => $"  {p.Key}: {TimeSpan.FromSeconds(p.Value):mm\\:ss}");
				return ItemPath + Environment.NewLine + "Last durations:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
			}
		}

		private void ObserveOperation(OperationInfo operation)
		{
			if (observedOperation != null)
				observedOperation.PropertyChanged -= OnOperationPropertyChanged;
			observedOperation = operation;
			if (operation != null)
				operation.PropertyChanged += OnOperationPropertyChanged;
		}

		private void OnOperationPropertyChanged(object sender, PropertyChangedEventArgs e) => OnUI(RaiseStatus);

		private void RaiseStatus()
		{
			RaisePropertyChanged(nameof(StatusText));
			RaisePropertyChanged(nameof(StatusBrush));
			RaisePropertyChanged(nameof(Progress));
			RaisePropertyChanged(nameof(IsIndeterminate));
			RaisePropertyChanged(nameof(IsBusy));
			RaisePropertyChanged(nameof(ResultText));
			RaisePropertyChanged(nameof(HasFailed));
			RaisePropertyChanged(nameof(HasSucceeded));
			RaisePropertyChanged(nameof(RowToolTip));
		}

		private void OnUI(Action action)
		{
			if (dispatcher.CheckAccess())
				action();
			else
				dispatcher.BeginInvoke(action);
		}

		private static Brush BrushFromName(string colorName)
		{
			try
			{
				var brush = (Brush)new BrushConverter().ConvertFromString(colorName ?? "Green");
				brush.Freeze();
				return brush;
			}
			catch (FormatException)
			{
				return Brushes.Gray;
			}
			catch (NotSupportedException)
			{
				return Brushes.Gray;
			}
		}

		private static string FirstLine(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;
			int index = text.IndexOfAny(new[] { '\r', '\n' });
			return index < 0 ? text : text.Substring(0, index);
		}
	}

	/// <summary>Editable row of the build properties grid.</summary>
	public class BuildPropertyRow
	{
		public string Key { get; set; }
		public string Value { get; set; }
	}

	/// <summary>One entry of the "Open with…" submenu.</summary>
	public sealed class OpenWithOption
	{
		public OpenWithOption(string header, ICommand command)
		{
			Header = header;
			Command = command;
		}

		public string Header { get; }
		public ICommand Command { get; }
	}
}
