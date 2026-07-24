using System.Windows.Controls;
using System.Windows.Input;
using CheckoutAndBuild.VisualStudio.ViewModels;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class WorkItemToolWindowControl : UserControl
	{
		private readonly WorkItemSearchReplaceViewModel viewModel = new WorkItemSearchReplaceViewModel();

		public WorkItemToolWindowControl()
		{
			InitializeComponent();
			DataContext = viewModel;
			patBox.Password = viewModel.Pat;
		}

		private void OnPatChanged(object sender, System.Windows.RoutedEventArgs e)
		{
			viewModel.Pat = patBox.Password;
		}

		private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var selected = ((ListView)sender).SelectedItem;
			if (selected != null)
				viewModel.OpenWorkItemCommand.Execute(selected);
		}
	}
}
