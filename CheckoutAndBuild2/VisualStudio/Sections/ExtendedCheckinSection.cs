using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildExtendedCheckinSection, TeamExplorerPageIds.PendingChanges, 11)]
	public class ExtendedCheckinSection : TeamExplorerBase
	{
		private const bool autoUpdateVisibility = false;
		private bool isUndoing;
		private PropertyChangedEventHandler busyChangedHandler;

		public override string Title => "Checkin Utils";

	    public DelegateCommand<object> UndoCommand { get; private set; }
		public DelegateCommand<object> ShowCheckinDialogCommand { get; private set; }
		public DelegateCommand<object> ShowRecentChangesCommand { get; private set; }

		public ExtendedCheckinSection()
		{
			UndoCommand = new DelegateCommand<object>("Undo All not changed Files", UndoAllNotChanged, CanUndo) {IconImage = Images.Arrow_UndoRevertRestore_16xLG.ToImageSource()};			
			ShowCheckinDialogCommand = new DelegateCommand<object>("Show traditional checkin dialog", ShowCheckinDialog, CanShowCheckinDlgUndo);
			ShowRecentChangesCommand = new DelegateCommand<object>("View all Recent Changes", o => serviceProvider.Get<ITeamExplorer>().NavigateToPage(GuidList.recentChangesPage.ToGuid(), null));
			IsExpanded = false;
		}

		private void ShowCheckinDialog(object o)
		{
			IPendingCheckinPendingChanges changes = ((IPendingCheckinPendingChanges)(((TeamExplorerPageBase)(TeamExplorer.CurrentPage)).Model));
			new List<IUICommand>
			{
				new DelegateCommand<object>("Included Changes Only",o1 => TeamControlFactory.ShowCheckinDialog(changes.Workspace, serviceProvider.Get<VersionControlExt>().PendingChanges.IncludedChanges)),
				new DelegateCommand<object>("Excluded Changes Only",o1 =>TeamControlFactory.ShowCheckinDialog(changes.Workspace,serviceProvider.Get<VersionControlExt>().PendingChanges.ExcludedChanges)),
				StaticCommands.Seperator,
				new DelegateCommand<object>("Included and Excluded Changes",o1 => TeamControlFactory.ShowCheckinDialog(changes.Workspace, changes.AllPendingChanges))
			}.ToContextMenu().IsOpen = true;
		}

		public bool IsUndoing
		{
			get => isUndoing;
		    set
			{
				if (SetProperty(ref isUndoing, value))
					UpdateCanExecute();
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
		    if (TeamExplorer?.CurrentPage != null)
		    {
		        busyChangedHandler = TeamExplorer.CurrentPage.OnChange(() => IsBusy, b => UpdateCanExecute(), true);
		    }
		}


		public override object Content => new ItemsControl { ItemsSource = Links, Margin = new Thickness(10, 4, 0, 0) };

	    public IEnumerable<TextLink> Links
		{
			get
			{
				yield return UndoCommand.ToTextLink();
				yield return ShowCheckinDialogCommand.ToTextLink();
				yield return ShowRecentChangesCommand.ToTextLink();
			}
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public override void Dispose()
		{
			base.Dispose();
			if (TeamExplorer != null && TeamExplorer.CurrentPage != null && busyChangedHandler != null)
				TeamExplorer.CurrentPage.PropertyChanged -= busyChangedHandler;
		}

		private Task<PendingChange[]> GetChangesToUndoAsync(bool included)
		{
			return Task.Run(() =>
			{
				var vs = serviceProvider.Get<VersionControlExt>();
				if (included)
				{
					if (vs.PendingChanges.IncludedChanges.Any())
						return vs.PendingChanges.IncludedChanges.Where(change => !change.HasReallyChange()).ToArray();
				}
				else
				{
					if (vs.PendingChanges.ExcludedChanges.Any())
						return vs.PendingChanges.ExcludedChanges.Where(change => !change.HasReallyChange()).ToArray();
				}
				return new PendingChange[0];
			});
		}

		private async void UndoAllNotChanged(object o)
		{
			try
			{
				IsUndoing = IsBusy = true;
				var changesToUndo = await GetChangesToUndoAsync(true);
				if (changesToUndo.Any())
				{
					List<DraggableListViewItem> uiElementsForChanges = new List<DraggableListViewItem>();

					Guid id = Guid.NewGuid();
					Workspace workspace = ((IPendingCheckinPendingChanges)(((TeamExplorerPageBase)(TeamExplorer.CurrentPage)).Model)).Workspace;
					var pageContent = TeamExplorer.CurrentPage.PageContent as FrameworkElement;
					if (pageContent != null && pageContent.Parent != null)
					{
						var contentControl = ViewUtility.FindAncestor<ContentControl>(pageContent.Parent);
						if (contentControl != null)
						{
							uiElementsForChanges = ViewUtility.FindDescendants<DraggableListViewItem>(contentControl).Where(item => item.Content != null && changesToUndo.Any(change => item.Content.ToString().Contains(Path.GetFileName(change.FileName)))).ToList();
							SetStyleForNotChangedItems(uiElementsForChanges, false);
						}
					}
					AsyncRelayCommand resetSettingsCommand = new AsyncRelayCommand(param =>
					{
						IsBusy = true;
						Output.HideNotification(id);
						return Task.Run(() =>
						{
							workspace.Undo(changesToUndo);
						}).ContinueWith(task => IsBusy = false);
					}, param => true);
					string changesToUndoString = Environment.NewLine + String.Join(Environment.NewLine, changesToUndo.Select(change => "- " + Path.GetFileName(change.FileName)));
					Output.Notification(string.Format("There are {0} Changes without any change  [Undo all {0} Changes](Click to undo this unchanged Files: {1} ).", changesToUndo.Length, changesToUndoString), NotificationType.Information, NotificationFlags.None, resetSettingsCommand, id);
					IsBusy = false;
					if (uiElementsForChanges.Any())
					{
						await Task.Run(() =>
						{
							while (TeamExplorer != null && TeamExplorer.IsNotificationVisible(id))
								Thread.Sleep(500);
						}).ContinueWith(task => SetStyleForNotChangedItems(uiElementsForChanges, true), System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
					}
				}
				else
					Output.Notification("All included changes are necessary");
			}
			finally
			{
				IsUndoing = IsBusy = false;
			}
		}

		private bool CanShowCheckinDlgUndo(object arg)
		{
			return !IsBusy && (TeamExplorer != null && TeamExplorer.CurrentPage != null && !TeamExplorer.CurrentPage.IsBusy);

		}
		private bool CanUndo(object arg)
		{
			return !IsUndoing && (TeamExplorer != null && TeamExplorer.CurrentPage != null && !TeamExplorer.CurrentPage.IsBusy);
		}

		private void UpdateCanExecute()
		{
			if (autoUpdateVisibility)
				IsVisible = IsUndoing || (TeamExplorer != null && TeamExplorer.CurrentPage != null && !TeamExplorer.CurrentPage.IsBusy);
			UndoCommand.RaiseCanExecuteChanged();
			ShowCheckinDialogCommand.RaiseCanExecuteChanged();
		}

		private void SetStyleForNotChangedItems(IEnumerable<DraggableListViewItem> items, bool reset)
		{
			foreach (DraggableListViewItem item in items)
				SetStyleForNotChangedItems(item, reset);
		}

		private void SetStyleForNotChangedItems(DraggableListViewItem item, bool reset)
		{
			if (reset)
			{
				item.Opacity = 1;
				item.FontStyle = FontStyles.Normal;
			}
			else
			{
				item.Opacity = 0.5;
				item.FontStyle = FontStyles.Italic;
			}
		}

		protected override void OnPageChanged(ITeamExplorerPage page)
		{
			if(page != null)
				page.OnChange(() => page.IsBusy, b => UpdateCanExecute(), true);
			UpdateCanExecute();
			if (IsEnabled) { 
				IPendingCheckinPendingChanges changes = ((IPendingCheckinPendingChanges)(((TeamExplorerPageBase)(TeamExplorer.CurrentPage)).Model));	
				ExtendPendingChangeSection(TeamExplorer.CurrentPage.GetSections().FirstOrDefault(section => section.GetType().Name.Contains("ChangesToIncludeSection")) as TeamExplorerSectionBase, () => serviceProvider.Get<VersionControlExt>().PendingChanges.IncludedChanges, changes.Workspace);
				ExtendPendingChangeSection(TeamExplorer.CurrentPage.GetSections().FirstOrDefault(section => section.GetType().Name.Contains("ChangesToExcludeSection")) as TeamExplorerSectionBase, () => serviceProvider.Get<VersionControlExt>().PendingChanges.ExcludedChanges, changes.Workspace);
			}
		}

		private void ExtendPendingChangeSection(TeamExplorerSectionBase teamExplorerSection, Func<PendingChange[]> getChangesFunc, Workspace workspace)
		{
			Check.TryCatch<Exception>(() =>
			{				
				if (teamExplorerSection != null && teamExplorerSection.View != null)
				{
					var stackPanel = teamExplorerSection.View.GetType().GetProperty("Content").GetValue(teamExplorerSection.View) as StackPanel;
					DropDownLink dropDownLink = stackPanel.FindDescendant<DropDownLink>();
					if (dropDownLink != null && dropDownLink.Parent is WrapPanel && stackPanel.FindDescendant<DropDownLink>(link => link.Tag == "PE") == null)
					{						
						var list = new List<IUICommand>
							{
								UndoCommand, 
								new DelegateCommand<object>("Quick Shelve", o=> QuickShelve(getChangesFunc())){IconImage = Images.Changeset_16xMD.ToImageSource()},
								new DelegateCommand<object>("Show traditional dialog", o1 => TeamControlFactory.ShowCheckinDialog(workspace, getChangesFunc())){ IconImage = Images.dialog_16xLG.ToImageSource() },
								new DelegateCommand<object>("Export Changes...", o1 => ExportChanges(getChangesFunc())){ IconImage = Images.ExportReportData_10565.ToImageSource() }
							};
					
						((WrapPanel)dropDownLink.Parent).Children.Add(new Border { Width = 1, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.Gray, Margin = new Thickness(4, 0, 4, 0) });						
						((WrapPanel)dropDownLink.Parent).Children.Add(new DropDownLink { Tag = "PE", Text = "More", DropDownMenu = list.ToContextMenu() });
					}
				}
			});
		}

		private Workspace GetCurrentPageWorkspace()
		{
			return Check.TryCatch<Workspace, Exception>(() =>
			{
				IPendingCheckinPendingChanges changes = ((IPendingCheckinPendingChanges)(((TeamExplorerPageBase)(TeamExplorer.CurrentPage)).Model));
				return changes.Workspace;
			});
		}

		private void QuickShelve(PendingChange[] pendingChanges)
		{
			var workspace = GetCurrentPageWorkspace() ?? TfsContext.SelectedWorkspace;
			var shelveName = string.Format(serviceProvider.Get<SettingsService>().Get(SettingsKeys.QuickShelvesetNameKey, "Save {0} {1}"), workspace.Name, DateTime.Now).Replace(new[] { ".", ":", ",", "!", "<", ">" }, "_");		

			var identity = TfsContext.VersionControlServer.AuthorizedIdentity;
			var shelveset = new Shelveset(TfsContext.VersionControlServer, shelveName, identity.UniqueName)
			{
				CreationDate = DateTime.Now,
				OwnerName = identity.UniqueName,
				OwnerDisplayName = identity.DisplayName,
				ChangesExcluded = false,
				Comment = string.Format("Quick Shelveset from CheckoutAndBuild for Workspace {0}", workspace.Name),
				Name = shelveName,				
			};
			workspace.Shelve(shelveset, pendingChanges, ShelvingOptions.None);

			Guid id = Guid.NewGuid();
			Output.Notification(string.Format("Shelveset [{0}](Click to view the created shelveset {0}) successfully created!", shelveName), NotificationType.Information, NotificationFlags.None, new RelayCommand(
				() =>
				{
					TeamExplorer.HideNotification(id);
					TeamExplorer.NavigateToPage(TeamExplorerPageIds.ShelvesetDetails.ToGuid(), shelveset);
				}), id);
		}

		private void ExportChanges(PendingChange[] changes)
		{
			if (changes.Any())
			{
				var dlg = new FolderBrowserDialog() { Description = "Export all changes in same structure to selected directory" };
				if (dlg.ShowDialog() == DialogResult.OK)
				{
					var targetBaseDir = dlg.SelectedPath;					
					foreach (var change in changes)
					{
						string target = Path.Combine(targetBaseDir, change.LocalItem.Substring(3));						
						FileHelper.EnsureDirectory(Path.GetDirectoryName(target));						
						if (File.Exists(change.LocalItem))
							File.Copy(change.LocalItem, target, true);
					}
					TeamExplorer.ShowNotification("Changes successfully exported!", NotificationType.Information, NotificationFlags.None, null, Guid.NewGuid());
				}
			}
		}

	}
}