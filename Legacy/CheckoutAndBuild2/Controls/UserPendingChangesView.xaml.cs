using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.VisualStudio.Classes;
using FG.CheckoutAndBuild2.VisualStudio.Sections;

namespace FG.CheckoutAndBuild2.Controls
{
	/// <summary>
	/// Interaction logic for UserPendingChangesView.xaml
	/// </summary>
	public partial class UserPendingChangesView
	{
		internal UserPendingChangesSection Section { get { return DataContext as UserPendingChangesSection; } }

		public UserPendingChangesView()
		{
			InitializeComponent();			
			fileListView.InnerListViewControl.SelectionChanged += InnerListViewControlOnSelectionChanged;
		}


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (!e.Handled)
				this.ReRouteMouseWheelEventToParent(e);
			base.OnPreviewMouseWheel(e);
		} 

		private void InnerListViewControlOnSelectionChanged(object sender, SelectionChangedEventArgs args)
		{
			if(Section == null)
				return;
			
			if (args.AddedItems != null && args.AddedItems.Count > 0)
			{
				Section.SelectedUserPendingChangesNodes.AddRange(args.AddedItems.OfType<PendingChangeTreeNode>());				
			}

			if (args.RemovedItems != null && args.RemovedItems.Count > 0)
			{
				foreach (var node in args.RemovedItems.OfType<PendingChangeTreeNode>())
				{
					Section.SelectedUserPendingChangesNodes.Remove(node);	
				}				
			}
		}
	}
}
