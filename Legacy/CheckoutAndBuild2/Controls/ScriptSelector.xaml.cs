using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.Win32;

namespace FG.CheckoutAndBuild2.Controls
{

	/// <summary>
	/// Interaction logic for ScriptSelector.xaml
	/// </summary>
	public partial class ScriptSelector
	{

		private PausableCancellationTokenSource cancellationTokenSource;

		#region Dependency Properties

		// Using a DependencyProperty as the backing store for IsExecuting.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty IsExecutingProperty =
			DependencyProperty.Register("IsExecuting", typeof(bool), typeof(ScriptSelector), new PropertyMetadata(false, IsExecutingChanged));

		private static void IsExecutingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			((ScriptSelector)dependencyObject).RaiseCanExecuteChanged();
		}

		// Using a DependencyProperty as the backing store for Watermark.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty WatermarkProperty =
			DependencyProperty.Register("Watermark", typeof (string), typeof (ScriptSelector),
				new PropertyMetadata("Select Script"));

		// Using a DependencyProperty as the backing store for FileName.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty FileNameProperty =
			DependencyProperty.Register("FileName", typeof (string), typeof (ScriptSelector), new PropertyMetadata("", PropertyChangedCallback));

		private static void PropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			((ScriptSelector)dependencyObject).RaiseCanExecuteChanged();
		}


		// Using a DependencyProperty as the backing store for SelectPathCommand.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty SelectPathCommandProperty =
			DependencyProperty.Register("SelectPathCommand", typeof (IUICommand), typeof (ScriptSelector),
				new PropertyMetadata(null));

		// Using a DependencyProperty as the backing store for ExecuteCommand.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ExecuteCommandProperty =
			DependencyProperty.Register("ExecuteCommand", typeof(IUICommand), typeof(ScriptSelector), new PropertyMetadata(null));

		// Using a DependencyProperty as the backing store for CancelCommand.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty CancelCommandProperty =
			DependencyProperty.Register("CancelCommand", typeof(IUICommand), typeof(ScriptSelector), new PropertyMetadata(null));


		#endregion

		public bool IsExecuting
		{
			get { return (bool)GetValue(IsExecutingProperty); }
			set { SetValue(IsExecutingProperty, value); }
		}

		public IUICommand CancelCommand
		{
			get { return (IUICommand)GetValue(CancelCommandProperty); }
			set { SetValue(CancelCommandProperty, value); }
		}

		public IUICommand ExecuteCommand
		{
			get { return (IUICommand)GetValue(ExecuteCommandProperty); }
			set { SetValue(ExecuteCommandProperty, value); }
		}

		public IUICommand SelectPathCommand
		{
			get { return (IUICommand)GetValue(SelectPathCommandProperty); }
			set { SetValue(SelectPathCommandProperty, value); }
		}

		public string Watermark
		{
			get { return (string)GetValue(WatermarkProperty); }
			set { SetValue(WatermarkProperty, value); }
		}

		public string FileName
		{
			get { return (string)GetValue(FileNameProperty); }
			set { SetValue(FileNameProperty, value); }
		}

		public ScriptSelector()
		{
			InitializeComponent();
			SelectPathCommand = new DelegateCommand<object>("...", ExecuteSelectPath, CanExecuteSelectPath);
			ExecuteCommand = new DelegateCommand<object>(ExecuteScript, CanExecuteScript);
			CancelCommand = new DelegateCommand<object>(CancelExecution, CanCancelExecution);
		}

		private bool CanCancelExecution(object o)
		{
			return IsExecuting && cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested;
		}

		private void CancelExecution(object o)
		{
			cancellationTokenSource.Cancel();
		}

		private bool CanExecuteScript(object o)
		{
			return File.Exists(FileName) && !IsExecuting;
		}

		private CancellationToken CreateCancellationToken()
		{
			if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested)
			{
				cancellationTokenSource = new PausableCancellationTokenSource();
				cancellationTokenSource.Token.Register(() => IsExecuting = false);
			}
			return cancellationTokenSource.Token;
		}

		private async void ExecuteScript(object o)
		{
			var cancellationToken = CreateCancellationToken();
			using (new PauseCheckedActionScope(() => IsExecuting = true, () => IsExecuting = false, cancellationToken))
			{
			    if (ScriptHelper.IsPowerShell(FileName))
			    {
                    ISolutionProjectModel[] models = CheckoutAndBuild2Package.GetGlobalService<MainViewModel>()
                   .IncludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects.OfType<ISolutionProjectModel>())
                   .OrderBy(model => model.BuildPriority).ToArray();

                    var logic = CheckoutAndBuild2Package.GetGlobalService<MainLogic>();
			        var result = await Check.TryCatchAsync<Collection<PSObject>, TaskCanceledException>(logic.ExecutePowershellScriptAsync(FileName, null, models , cancellationToken).IgnoreCancellation(cancellationToken), cancellation: cancellationToken);
                    Output.WriteLine("Script Result: " + result);
                }
                else { 
				    var result = await Check.TryCatchAsync<ScriptExecutingResult, TaskCanceledException>(ScriptHelper.ExecuteScriptAsync(FileName, string.Empty, ScriptExecutionSettings.Default, cancellationToken: cancellationToken).IgnoreCancellation(cancellationToken), cancellation: cancellationToken);
				    Output.WriteLine("Script Result: " + result.ProcessResult);
                }
			}
		}

		private bool CanExecuteSelectPath(object o)
		{
			return !IsExecuting;
		}

		private void RaiseCanExecuteChanged()
		{
			SelectPathCommand.RaiseCanExecuteChanged();
			ExecuteCommand.RaiseCanExecuteChanged();
			CancelCommand.RaiseCanExecuteChanged();
		}

		private void ExecuteSelectPath(object o)
		{
			using (new PopupStaysOpenScope(this.FindAncestor<Popup>()))
            {
                OpenFileDialog openFileDialog = new OpenFileDialog { CheckFileExists = true, FileName = FileName};
	            if (!string.IsNullOrEmpty(FileName))
	            {
		            var dir = Path.GetDirectoryName(FileName);
					if (dir != null && Directory.Exists(dir))
			            openFileDialog.InitialDirectory = dir;
	            }
                if (openFileDialog.ShowDialog() ?? true)
                    FileName = (openFileDialog.FileName);
            }
		}
	}
}
