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

		public SolutionViewModel(SolutionProjectModel model, MainViewModel owner, Dispatcher dispatcher)
		{
			Model = model ?? throw new ArgumentNullException(nameof(model));
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

			serviceOverrides = owner.Settings.Get<Dictionary<string, bool>>(ServicesKey, owner.ProfileContext)
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
			OpenInExplorerCommand = new DelegateCommand(
				() => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ItemPath}\""));
			OpenOutputDirectoryCommand = new DelegateCommand(
				() => System.Diagnostics.Process.Start("explorer.exe", $"\"{FirstExistingOutputPath()}\""),
				() => FirstExistingOutputPath() != null);
			ShowHistoryCommand = new DelegateCommand(
				() => CheckoutAndBuildPackage.Instance?.ShowGitHistory(Model.GitRepositoryRoot),
				() => Model.GitRepositoryRoot != null);
			CopyFullPathCommand = new DelegateCommand(() => Clipboard.SetText(ItemPath));
		}

		/// <summary>Opens the solution in this Visual Studio instance.</summary>
		private void OpenSolution()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			var solution = Microsoft.VisualStudio.Shell.Package.GetGlobalService(
				typeof(Microsoft.VisualStudio.Shell.Interop.SVsSolution)) as Microsoft.VisualStudio.Shell.Interop.IVsSolution;
			solution?.OpenSolutionFile(0, ItemPath);
		}

		/// <summary>First project output directory (Debug) that exists on disk, or null.</summary>
		private string FirstExistingOutputPath()
		{
			return Model.Projects
				.Select(p => p.OutputPath)
				.FirstOrDefault(p => !string.IsNullOrEmpty(p) && Directory.Exists(p));
		}

		public SolutionProjectModel Model { get; }

		public string SolutionFileName => Model.SolutionFileName;

		public string ItemPath => Model.ItemPath;

		public bool IsIncluded
		{
			get { return Model.IsIncluded; }
			set { Model.IsIncluded = value; }
		}

		/// <summary>Lower value builds earlier ("higher" priority).</summary>
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

		/// <summary>Effective services of this solution, comma separated (old ServicesCaption).</summary>
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

		/// <summary>Caption truncated to 40 chars for the row link (old ServicesCaptionSmall).</summary>
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
			owner.Settings.Set(ServicesKey, owner.ProfileContext, serviceOverrides);
			RefreshServiceFlags();
		}

		/// <summary>Re-raises the effective service flags (called when the global step checkboxes change).</summary>
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

		/// <summary>Re-reads the profile-scoped per-solution state after a profile switch.</summary>
		internal void ReloadProfileScopedState()
		{
			serviceOverrides = owner.Settings.Get<Dictionary<string, bool>>(ServicesKey, owner.ProfileContext)
				?? new Dictionary<string, bool>();
			ApplyBuildOptionsToModel();
			RefreshServiceFlags();
			RaisePropertyChanged(nameof(BuildPropertiesCaption));
			RaisePropertyChanged(nameof(BuildTargetsCaption));
		}

		/// <summary>Pushes the persisted build properties/targets into the pipeline model.</summary>
		private void ApplyBuildOptionsToModel()
		{
			var properties = owner.Settings.Get<Dictionary<string, string>>(BuildPropertiesKey, owner.ProfileContext);
			Model.BuildProperties.Clear();
			foreach (var pair in properties ?? new Dictionary<string, string>())
				Model.BuildProperties[pair.Key] = pair.Value;
			Model.SetBuildTargets(owner.Settings.Get<List<string>>(BuildTargetsKey, owner.ProfileContext));
		}

		/// <summary>Key/value grid dialog; saved on close (old DictionaryEdit behavior).</summary>
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
			owner.Settings.Set(BuildPropertiesKey, owner.ProfileContext, result);
			ApplyBuildOptionsToModel();
			RaisePropertyChanged(nameof(BuildPropertiesCaption));
		}

		/// <summary>Comma-separated targets textbox dialog; saved on close.</summary>
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
			owner.Settings.Set(BuildTargetsKey, owner.ProfileContext, targets);
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

		/// <summary>True when the last result is a failure (exception, failed build or failed tests).</summary>
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

		/// <summary>True when there is a result and it is not a failure.</summary>
		public bool HasSucceeded => !HasFailed && (Model.Result ?? Model.ErrorContent) != null;

		/// <summary>Short text for the result of the last operation.</summary>
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
		public ICommand OpenInExplorerCommand { get; }
		public ICommand OpenOutputDirectoryCommand { get; }
		public ICommand ShowHistoryCommand { get; }
		public ICommand CopyFullPathCommand { get; }

		/// <summary>Re-raises the result/status properties (model does not notify on SetResult).</summary>
		public void RefreshResult() => OnUI(RaiseStatus);

		public void Detach() => Model.PropertyChanged -= OnModelPropertyChanged;

		private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			OnUI(() =>
			{
				switch (e.PropertyName)
				{
					case nameof(SolutionProjectModel.CurrentOperation):
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
}
