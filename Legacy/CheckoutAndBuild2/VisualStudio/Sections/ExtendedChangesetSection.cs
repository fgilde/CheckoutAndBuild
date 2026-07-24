using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildExtendedChangesetSection, TeamExplorerPageIds.ChangesetDetails, 10)]
	public class ExtendedChangesetSection : TeamExplorerBase
	{		
		public override string Title => "Utils";

	    public DelegateCommand<object> ShowDetailDialogCommand { get; private set; }
		public ExtendedChangesetSection()
		{
			ShowDetailDialogCommand = new DelegateCommand<object>("Show traditional details dialog", ShowChangesetDetailDialog, CanShowDetailDialog);
		}

		private void ShowChangesetDetailDialog(object o)
		{
			var changeset = TeamExplorer.CurrentPage.FindChangesetFromTeamExplorerPage();
			if (changeset != null)
			{
				IPendingCheckinPendingChanges changes = ((IPendingCheckinPendingChanges) (((TeamExplorerPageBase) (TeamExplorer.CurrentPage)).Model));
				Workspace workspace = changes.Workspace;
				TeamControlFactory.ShowDialogChangesetDetails(workspace, changeset.ChangesetId);
			}
		}
		

		/// <summary>
		/// Refresh this page.
		/// </summary>
		public override void Refresh()
		{
			base.Refresh();
			UpdateCanExecute();
		}

		public override void Initialize(object sender, IServiceProvider provider, object context)
		{
			base.Initialize(sender, provider, context);
			UpdateCanExecute();
		}

		protected override void OnPageChanged(ITeamExplorerPage page)
		{
			if (IsEnabled)
			{				
				ExtendChangesetDetailPage(page);			
			}
		}

		public override object Content => new ItemsControl { ItemsSource = Links, Margin = new Thickness(10, 4, 0, 0) };

	    public IEnumerable<TextLink> Links
		{
			get
			{
				yield return ShowDetailDialogCommand.ToTextLink();
			}
		}

		private async void ExtendChangesetDetailPage(ITeamExplorerPage page)
		{
			await Task.Delay(1000);
			Check.TryCatch<Exception>(() =>
			{
				var grid = ((dynamic)ExposedObject.From(TeamExplorer.CurrentPage.PageContent)).Content as Grid;
				if (grid != null)
				{					
					var header = grid.FindDescendants<FlowDocumentScrollViewer>(viewer => viewer.Uid == "changesetPageHeader").FirstOrDefault();
					if (header != null)
					{
						var changeset = page.FindChangesetFromTeamExplorerPage();
						
						if (changeset != null)
						{
							header.Visibility = Visibility.Collapsed;

							var stackPanel = new StackPanel {Orientation = Orientation.Vertical, Margin = new Thickness(2,3,0,3)};
							stackPanel.Children.Add(new TextBox
							{
								Text = string.Format("Changeset {0}", changeset.ChangesetId),
								IsReadOnly = true,
								Background = System.Windows.Media.Brushes.Transparent,
								BorderThickness = new Thickness(0),
								FontWeight = FontWeights.SemiBold,
								FontSize = 16
							});
							var stackPanel2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
							stackPanel2.Children.Add(new TextLink
							{
								Margin = new Thickness(4, 5, 0, 4),
								Text = string.Format(changeset.CommitterDisplayName),
								Background = System.Windows.Media.Brushes.Transparent,
								BorderThickness = new Thickness(0),
								Command = StaticCommands.UserInfoCommand,
								CommandParameter = changeset.Committer
							});
							stackPanel2.Children.Add(new TextBox
							{
								Margin = new Thickness(2,4,0,4),
								Text = string.Format(changeset.CreationDate.ToString(CultureInfo.CurrentCulture)),
								Background = System.Windows.Media.Brushes.Transparent,
								BorderThickness = new Thickness(0),
								IsReadOnly = true
							});
							stackPanel.Children.Add(stackPanel2);
							grid.Children.Add(stackPanel);
							Grid.SetColumn(stackPanel, 0);
						}					
					}
				}				
			});
		}

		private bool CanShowDetailDialog(object arg)
		{
			return !IsBusy && (TeamExplorer != null && TeamExplorer.CurrentPage != null && !TeamExplorer.CurrentPage.IsBusy);

		}
		
		private void UpdateCanExecute()
		{
			ShowDetailDialogCommand.RaiseCanExecuteChanged();
		}

	}
}