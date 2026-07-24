using System;
using System.Windows;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Controls;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.TeamFoundation.Build;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for BuildsSectionView.xaml
	/// </summary>
	public partial class BuildsSectionView
	{
		public BuildsSectionView()
		{
			InitializeComponent();
		}

		private void BuildList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var detail = buildList.SelectedItem as IBuildDetail;
			if (detail != null)
			{
				VsTeamFoundationBuild vsTfBuild = (VsTeamFoundationBuild)CheckoutAndBuild2Package.GetGlobalService<IVsTeamFoundationBuild>();
				if (vsTfBuild != null)
					vsTfBuild.DetailsManager.OpenBuild(detail.Uri);
			}
		}

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		}

		private void viewAllClick(object sender, RoutedEventArgs e)
		{
			var tfs = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();
			VsTeamFoundationBuild vsTfBuild = CheckoutAndBuild2Package.GetGlobalService<VsTeamFoundationBuild>();
			if (vsTfBuild != null)
				vsTfBuild.BuildExplorer.CompletedView.Show(tfs.TfsContextManager.CurrentContext.TeamProjectName, string.Empty, string.Empty, DateFilter.All);					
		}

		private void queueNewBuildClick(object sender, RoutedEventArgs e)
		{
			TeamControlFactory.ShowDialogQueueBuild();
		}

		private void navigateToBuildsClick(object sender, RoutedEventArgs e)
		{
			CheckoutAndBuild2Package.GetGlobalService<ITeamExplorer>().NavigateToPage(TeamExplorerPageIds.Builds.ToGuid(), null);
		}
	}
}
