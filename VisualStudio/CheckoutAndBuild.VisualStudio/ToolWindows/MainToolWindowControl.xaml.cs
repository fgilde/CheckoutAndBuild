using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CheckoutAndBuild.VisualStudio.ViewModels;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class MainToolWindowControl : UserControl
	{
		private readonly MainViewModel viewModel = new MainViewModel();

		public MainToolWindowControl()
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			InitializeComponent();
			viewModel.ErrorSink = CheckoutAndBuildPackage.Instance?.ErrorListProvider;
			DataContext = viewModel;
			Loaded += async (sender, e) => await viewModel.LoadAsync();
		}

		/// <summary>Opens the global settings view (used by the Tools → Options page).</summary>
		internal void ShowSettings() => viewModel.OpenGlobalSettings();

		/// <summary>Opens the "More" drop-down (context menu) below the toolbar button.</summary>
		private void OnMoreClick(object sender, RoutedEventArgs e)
		{
			var button = (Button)sender;
			button.ContextMenu.PlacementTarget = button;
			button.ContextMenu.Placement = PlacementMode.Bottom;
			button.ContextMenu.IsOpen = true;
		}
	}
}
