using System.Windows.Controls;
using CheckoutAndBuild.VisualStudio.ViewModels;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class MainToolWindowControl : UserControl
	{
		private readonly MainViewModel viewModel = new MainViewModel();

		public MainToolWindowControl()
		{
			InitializeComponent();
			DataContext = viewModel;
			Loaded += async (sender, e) => await viewModel.LoadAsync();
		}
	}
}
