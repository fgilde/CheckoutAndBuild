using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CheckoutAndBuild.VisualStudio.ViewModels;

namespace CheckoutAndBuild.VisualStudio.ToolWindows
{
	public partial class MainToolWindowControl : UserControl
	{
		private readonly MainViewModel viewModel = new MainViewModel();
		private DateTime lastServicesPopupClose;

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

		/// <summary>
		/// Opens the per-solution services popover. StaysOpen=False closes the popup on the mouse-down
		/// of the very click that should toggle it shut, so a click arriving right after a close is
		/// swallowed instead of instantly reopening (old ProjectViewModel.canOpenPopup behavior).
		/// </summary>
		private void OnServicesLinkClick(object sender, RoutedEventArgs e)
		{
			if (!((sender as FrameworkElement)?.Tag is Popup popup))
				return;
			if ((DateTime.UtcNow - lastServicesPopupClose).TotalMilliseconds < 250)
				return;
			popup.Closed -= OnServicesPopupClosed;
			popup.Closed += OnServicesPopupClosed;
			popup.IsOpen = true;
		}

		private void OnServicesPopupClosed(object sender, EventArgs e) => lastServicesPopupClose = DateTime.UtcNow;

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
