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

		internal System.Threading.Tasks.Task ShowHistoryAsync(string repositoryPath) => viewModel.ShowHistoryAsync(repositoryPath);

		internal System.Threading.Tasks.Task ShowWorktreesAsync(string repositoryPath) => viewModel.ShowWorktreesAsync(repositoryPath);

		internal System.Threading.Tasks.Task ShowRepositoryAsync(string repositoryPath) => viewModel.ShowRepositoryAsync(repositoryPath);

		private async void OnFeedDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var feedCommit = ((ListBox)sender).SelectedItem as FeedCommitViewModel;
			if (feedCommit != null)
				await viewModel.ShowCommitInHistoryAsync(feedCommit);
		}

		private void OnChangeDoubleClick(object sender, MouseButtonEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var change = ((ListBox)sender).SelectedItem as ChangeViewModel;
			if (change != null && File.Exists(change.FullPath))
				VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, change.FullPath);
		}
	}
}
