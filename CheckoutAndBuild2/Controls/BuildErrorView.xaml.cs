using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for BuildErrorView.xaml
	/// </summary>
	public partial class BuildErrorView
	{
		BuildErrorsViewModel Model { get { return DataContext as BuildErrorsViewModel; } }

		public BuildErrorView()
		{
			InitializeComponent();
		}

		private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = (sender) as ListBox;
			if (lb != null)
			{
				var task = lb.SelectedItem as ErrorTask;
				if (task != null)
					VisualStudioDTE.TryOpenFile(task.Document, task.Line, task.HierarchyItem as SimpleVsHierarchy);
				
				//	var invoker = ReflectionHelper.GetEventInvoker(task, "Navigate");
				//	if (invoker != null)
				//	{
				//		invoker.Invoke(task, new object[] { task, null });
				//	}
			}
		}

		private void retryOperation_Click(object sender, RoutedEventArgs e)
		{
			if (Model != null && Model.Project != null)
			{				
				CheckoutAndBuild2Package.GetGlobalService<MainLogic>().ExecuteService(Model.RequestedOperation, new [] {Model.Project});				
			}
		}
	}
}
