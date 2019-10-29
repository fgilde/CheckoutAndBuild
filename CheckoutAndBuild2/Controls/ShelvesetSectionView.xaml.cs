using System;
using System.Windows;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for ShelvesetSectionView.xaml
	/// </summary>
	public partial class ShelvesetSectionView
	{
		private ShelvesetSection Section { get { return DataContext as ShelvesetSection; } }

		public ShelvesetSectionView()
		{
			InitializeComponent();
		}

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

		private void viewAllClick(object sender, RoutedEventArgs e)
		{
			CheckoutAndBuild2Package.GetGlobalService<VersionControlExt>().FindShelvesets(Section.UserContext.UserName, null);			
		}

		private void ViewSelectedDetails()
		{
			var shelveset = shelveSetView.SelectedItem as Shelveset;
			if (shelveset != null)
				Section.TeamExplorer.NavigateToPage(TeamExplorerPageIds.ShelvesetDetails.ToGuid(), shelveset);
		}

		private void shelvesetListOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			ViewSelectedDetails();
		}

		private void ViewDetailsMenuItemOnClick(object sender, RoutedEventArgs e)
		{
			ViewSelectedDetails();
		}

		private void UnshelveClick(object sender, RoutedEventArgs e)
		{			
			var shelveset = shelveSetView.SelectedItem as Shelveset;
			if (shelveset != null && Section.TfsContext.IsTfsConnected)
				Section.TfsContext.SelectedWorkspace.Unshelve(shelveset.Name, shelveset.OwnerDisplayName);
		}
	}
}
