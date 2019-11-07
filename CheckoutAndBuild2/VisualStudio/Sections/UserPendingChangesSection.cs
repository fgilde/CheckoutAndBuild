using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio.Classes;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.userPendingChangesSectionId, GuidList.userInfoPage, 1200)]		
	public class UserPendingChangesSection : TeamExplorerBase, IUserContextSection
	{
		internal static readonly Guid HatPackageCommandSetGuid = new Guid("{ffe1131c-8ea1-4d05-9728-34ad4611bda9}");

		private Workspace selectedUserWorkspace;
		private ObservableCollection<PendingChangeTreeNode> selectedUserPendingChangesNodes;

		public UserInfoContext UserContext { get; set; }

		public ObservableCollection<Workspace> UserWorkspaces { get; }
		public ObservableCollection<PendingChange> UserPendingChanges { get; }
		public ObservableCollection<PendingChangeTreeNode> UserPendingChangesNodes { get; }

		public bool HasItems => UserPendingChangesNodes != null && UserPendingChangesNodes.Any();

	    public ObservableCollection<PendingChangeTreeNode> SelectedUserPendingChangesNodes
		{
			get => selectedUserPendingChangesNodes;
	        set
			{
				if(SetProperty(ref selectedUserPendingChangesNodes, value))
					UpdateVSSelection();
			}
		}

		private void UpdateVSSelection()
		{
			VisualStudioTrackingSelection.UpdateSelectionTracking(selectedUserPendingChangesNodes.Select(node => node).ToArray());
		}

		public ICommand ShowContextMenuCommand { get; private set; }

		public UserPendingChangesSection()
		{
			UserWorkspaces = new ObservableCollection<Workspace>();
			UserPendingChanges = new ObservableCollection<PendingChange>(); 
			UserPendingChangesNodes = new ObservableCollection<PendingChangeTreeNode>();
			SelectedUserPendingChangesNodes = new ObservableCollection<PendingChangeTreeNode>();
			SelectedUserPendingChangesNodes.CollectionChanged += (sender, args) => UpdateVSSelection();
			ShowContextMenuCommand = new RelayCommand(ShowContextMenu, p => true);
		}

		public ContextMenu ContextMenu => ContextMenuCommands.ToContextMenu();

	    public IEnumerable<IUICommand> ContextMenuCommands
		{
			get
			{
				yield return new DelegateCommand<object>("View...", o => ViewSelectedChanges());
			}
		}


		public void ViewSelectedChanges()
		{
			if (SelectedUserPendingChangesNodes.Any())
				VisualStudioDTE.ViewPendingChanges(SelectedUserWorkspace, SelectedUserPendingChangesNodes.Select(node => node.PendingChange).ToList());
		}
	
		public override string Title => $"Pending Changes for {UserContext.UserName}";

	    public override object Content => this;

	    public string SelectedUserWorkspaceName => SelectedUserWorkspace != null ? SelectedUserWorkspace.Name : "Select workspace";

	    public Workspace SelectedUserWorkspace
		{
			get { return selectedUserWorkspace; }
			set
			{
				if (SetProperty(ref selectedUserWorkspace, value))
				{
					RaisePropertyChanged(() => SelectedUserWorkspaceName);
					UpdatePendingChanges();
				}
			}
		}

		public override async void Refresh()
		{
			base.Refresh();
			await RefreshAsync();
		}

		public ContextMenu SelectUserWorkspaceMenu
		{
			get
			{
				if (UserWorkspaces.Any())
					return UserWorkspaces.Select(w => new DelegateCommand<object>(w.Name, o => SelectedUserWorkspace = w)).ToContextMenu();				
				return null;
			}
		}

		protected override async void ContextChanged(object sender, ContextChangedEventArgs e)
		{
			base.ContextChanged(sender, e);
			if (e.TeamProjectCollectionChanged || e.TeamProjectChanged)
				await RefreshAsync();
		}
		
		public override async void Initialize(object sender, IServiceProvider provider, object context)
		{			
			base.Initialize(sender, provider, context);
			IsBusy = true;
			if (UserContext == null)
				UserContext = new UserInfoContext(TfsContext.VersionControlServer?.AuthorizedIdentity);
			await RefreshAsync();
		}


		#region Private Methods

		private async Task RefreshAsync()
		{
			UserWorkspaces.Clear();
			if (!IsEnabled)
			{
				IsBusy = false;
				return;
			}
			try
			{
				UserWorkspaces.AddRange(
					(await TfsContext.GetWorkspacesAsync(UserContext.UserName, null)).Where(workspace => !workspace.IsLocal).ToArray());
				IsVisible = UserWorkspaces.Any();
				if (IsVisible)
				{
					if (SelectedUserWorkspace == null || !UserWorkspaces.Contains(SelectedUserWorkspace))
						SelectedUserWorkspace = UserWorkspaces.FirstOrDefault();
					RaisePropertyChanged(() => SelectUserWorkspaceMenu);
				}
			}
			finally
			{
				IsBusy = false;
			}
		}


		private void ShowContextMenu(object param)
		{
			//VisualStudioDTE.ShowContextMenu(HatPackageCommandSetGuid, 4247, (System.Windows.Point)param, null);	
			ContextMenu.IsOpen = true;
		}

		private void UpdatePendingChanges()
		{
			UserPendingChanges.Clear();
			UserPendingChangesNodes.Clear();
			if (SelectedUserWorkspace != null)
			{
				UserPendingChanges.AddRange(SelectedUserWorkspace.GetPendingChanges());
				UserPendingChangesNodes.AddRange(UserPendingChanges.Select(change => new PendingChangeTreeNode(change, false)));
			}
			RaisePropertyChanged(() => HasItems);
		}

		#endregion


	}
}