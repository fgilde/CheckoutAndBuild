using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Client.CommandTarget;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.WorkItemTracking.Controls;
using Microsoft.TeamFoundation.WorkItemTracking.WpfControls;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for WorkItemsSection.xaml
	/// </summary>
	public partial class WorkItemsSectionView
	{
		public WorkItemsSection Section => DataContext as WorkItemsSection;
	    private readonly TfsContext tfsContext;
		public WorkItemsSectionView()
		{
			InitializeComponent();
			tfsContext = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();		    
		    workItemList.ContextMenuOpening += WorkItemListOnContextMenuOpening;

        }

	    private void WorkItemListOnContextMenuOpening(object sender, ContextMenuEventArgs e)
	    {            
	        //WorkItemsListViewHelper.Instance.ShowContextMenu(workItemList, workItemList.PointToScreen(Mouse.GetPosition(workItemList)));
        }

	    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{			
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		} 

		private void viewQueriesClick(object sender, RoutedEventArgs e)
		{
			TeamExplorerUtils.Instance.TryNavigateToWorkItems(CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>());
		}


		private void showMyUserInfoClick(object sender, RoutedEventArgs e)
		{			
			TeamExplorerUtils.Instance.NavigateToPage(GuidList.userInfoPage, tfsContext.ServiceProvider, new UserInfoContext(tfsContext.VersionControlServer?.AuthorizedIdentity));
		}

		private void WorkItemList_OnMouseMove(object sender, MouseEventArgs e)
		{
			var listBox = workItemList.InnerListBoxControl;
			if (e.LeftButton == MouseButtonState.Pressed && e.MouseDevice.DirectlyOver is UIElement && listBox.SelectedItems.Count > 0)
			{
				var items = listBox.SelectedItems.AsWorkItems().ToList();
				if (items.Any())
				{
					WorkItemDropData dropData = new WorkItemDropData(tfsContext.VersionControlServer.ServerGuid, items.Select(item => item.Id).ToArray());
					var data = new DataObject("Microsoft.TeamFoundation.WorkItemId", dropData);
					DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.All);
				}
			}
		}

		private void UpdateSelectionInModel()
		{
			Section.SelectedWorkItems.Clear();
			Section.SelectedWorkItems.AddRange(workItemList.InnerListBoxControl.SelectedItems.OfType<WorkItemValueProvider>());
			VisualStudioTrackingSelection.UpdateSelectionTracking(Section.SelectedWorkItems.Select(provider => provider.WorkItem).ToArray());
		}

		private void WorkItemList_OnKeyUp(object sender, KeyEventArgs e)
		{
			UpdateSelectionInModel();
		}

		private void WorkItemList_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			UpdateSelectionInModel();
		}

		private void WorkItemList_OnMouseUp(object sender, MouseButtonEventArgs e)
		{
			UpdateSelectionInModel();	
		}
	}
}
