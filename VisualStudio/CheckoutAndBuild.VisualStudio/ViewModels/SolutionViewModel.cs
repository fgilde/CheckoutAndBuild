using System;
using System.ComponentModel;
using System.Linq;
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

		public SolutionViewModel(SolutionProjectModel model, MainViewModel owner, Dispatcher dispatcher)
		{
			Model = model ?? throw new ArgumentNullException(nameof(model));
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

			model.PropertyChanged += OnModelPropertyChanged;
			ObserveOperation(model.CurrentOperation);

			BuildOnlyCommand = new DelegateCommand(async () => await owner.RunSingleServiceAsync(this, owner.BuildOperation), () => !owner.IsRunning);
			CleanOnlyCommand = new DelegateCommand(async () => await owner.RunSingleServiceAsync(this, owner.CleanOperation), () => !owner.IsRunning);
			TestOnlyCommand = new DelegateCommand(async () => await owner.RunSingleServiceAsync(this, owner.TestOperation), () => !owner.IsRunning);
			IncreasePriorityCommand = new DelegateCommand(() => BuildPriority = Math.Max(0, BuildPriority - 1), () => !owner.IsRunning && BuildPriority > 0);
			DecreasePriorityCommand = new DelegateCommand(() => BuildPriority = BuildPriority + 1, () => !owner.IsRunning);
			SettingsCommand = new DelegateCommand(() => owner.OpenSolutionSettings(this));
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

		public bool IsBusy => Model.IsBusy;

		public string StatusText => Model.CurrentOperation?.StatusText ?? string.Empty;

		public double Progress => Model.CurrentOperation?.Progress ?? 0;

		public bool IsIndeterminate => Model.CurrentOperation?.IsIndeterminate ?? false;

		public Brush StatusBrush
		{
			get
			{
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

		public ICommand BuildOnlyCommand { get; }
		public ICommand CleanOnlyCommand { get; }
		public ICommand TestOnlyCommand { get; }
		public ICommand IncreasePriorityCommand { get; }
		public ICommand SettingsCommand { get; }
		public ICommand DecreasePriorityCommand { get; }

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
}
