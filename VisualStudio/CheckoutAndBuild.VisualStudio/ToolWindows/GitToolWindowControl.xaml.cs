using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using CheckoutAndBuild.VisualStudio.ViewModels;
using Microsoft.VisualStudio.Shell;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class GitToolWindowControl : UserControl
	{
		private readonly GitViewModel viewModel = new GitViewModel();

		public GitToolWindowControl()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			InitializeComponent();
			DataContext = viewModel;
			Loaded += async (sender, e) => await viewModel.LoadAsync();
		}

		/// <summary>Double-click on a change opens the file in the editor.</summary>
		private void OnChangeDoubleClick(object sender, MouseButtonEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var change = ((ListBox)sender).SelectedItem as ChangeViewModel;
			if (change != null && File.Exists(change.FullPath))
				VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, change.FullPath);
		}
	}
}
