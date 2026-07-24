using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Controls;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.TeamFoundation.Build;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.buildDetailsSectionId, TeamExplorerPageIds.ChangesetDetails, 1000)]
	[TeamExplorerSectionPlacement(GuidList.userInfoPage, 1000)]
	[TeamExplorerSectionPlacement(TeamExplorerPageIds.Builds, 1000)]
	public class BuildDetailsSection : TeamExplorerBase, IUserContextSection
	{
		#region Private Backingfields
		
		private ObservableCollection<IBuildDetail> buildDetails;
		private bool eventRegistered;
		private string title = "Last Triggered Builds";
		private IBuildDetail selectedBuildDetail;

		#endregion

		public DelegateCommand<string> UserInfoCommand { get; private set; }

		public BuildDetailsSection()
		{
			UserInfoCommand = new DelegateCommand<string>(NavigateToUser, CanNavigateToUser);			
		}

		private bool CanNavigateToUser(string userName)
		{
			return UserContext == null || (UserContext.Identity.DisplayName != userName && UserContext.Identity.UniqueName != userName);
		}

		private void NavigateToUser(string userName)
		{						
			TfsContext tfsContext = TfsContext;
			var teamFoundationIdentity = tfsContext.IdentityManager.GetIdentity(userName);
			if (teamFoundationIdentity != null && (UserContext == null || UserContext.Identity != teamFoundationIdentity))
			{				
				TeamExplorer.NavigateToPage(GuidList.userInfoPage.ToGuid(), UserContext = new UserInfoContext(teamFoundationIdentity));				
			}
		}


		public override string Title
		{
			get { return title; }
		}

		public override object Content
		{
			get { return this; }
		}
		
		public bool IsInUserInfoPage { get { return UserContext != null; } }

		public bool IsUserNameVisible { get { return !IsInUserInfoPage; } }

		public ContextMenu ContextMenu
		{
			get { return ContextMenuCommands.ToList().ToContextMenu(); }
		}

		public IEnumerable<DelegateCommand<object>> ContextMenuCommands
		{
			get
			{
				yield return new DelegateCommand<object>("View Build Detail", ViewBuildDetail) { IconImage = Properties.Images.BuildDefinition_13065.ToImageSource()};
				yield return new DelegateCommand<object>("Edit Build Definition", ViewBuildDefinition) { IconImage = Properties.Images.Editdatasetwithdesigner_8449.ToImageSource()};
				yield return new DelegateCommand<object>("-", o => { }) {IsSeparator = true};
				yield return new DelegateCommand<object>("Delete Build Detail", DeleteBuildDetail) { IconImage = Properties.Images.RemoveParameters_6781.ToImageSource() };
			}
		}

		private async void DeleteBuildDetail(object obj)
		{
			var buildDeletionResult = SelectedBuildDetail.Delete();
			if (buildDeletionResult.Successful)
				await RefreshAsync();
		}

		private void ViewBuildDefinition(object obj)
		{
			VsTeamFoundationBuild vsTfBuild = (VsTeamFoundationBuild)serviceProvider.Get<IVsTeamFoundationBuild>();
			if (vsTfBuild != null)
				vsTfBuild.DefinitionManager.OpenDefinition(SelectedBuildDetail.BuildDefinition.Uri);
		}

		private void ViewBuildDetail(object obj)
		{
			VsTeamFoundationBuild vsTfBuild = (VsTeamFoundationBuild)serviceProvider.Get<IVsTeamFoundationBuild>();
			if (vsTfBuild != null)
				vsTfBuild.DetailsManager.OpenBuild(SelectedBuildDetail.Uri);
		}


		public IBuildDetail SelectedBuildDetail
		{
			get { return selectedBuildDetail; }
			set
			{
				if(SetProperty(ref selectedBuildDetail, value))
					VisualStudioTrackingSelection.UpdateSelectionTracking(SelectedBuildDetail);	
			}
		}

		public ObservableCollection<IBuildDetail> BuildDetails
		{
			get { return buildDetails; }
			set
			{
				if(SetProperty(ref buildDetails, value))
					RaisePropertyChanged(() => HasItems);
			}
		}

		public bool HasItems { get { return BuildDetails != null && BuildDetails.Any(); } }

		public UserInfoContext UserContext { get; set; }

		public async override void Refresh()
		{
			base.Refresh();
			await RefreshAsync();
		}

		/// <summary>
		/// ContextChanged event handler.
		/// </summary>
		protected async override void ContextChanged(object sender, ContextChangedEventArgs e)
		{
			base.ContextChanged(sender, e);
			if (e.TeamProjectCollectionChanged || e.TeamProjectChanged)
				await RefreshAsync();
		}


		public async override void Initialize(object sender, IServiceProvider provider, object context)
		{
			base.Initialize(sender, provider, context);
			IsBusy = true;
			if (UserContext != null)
			{
				await RefreshAsync();
			}
			else if (!eventRegistered)
			{
				TeamExplorer.PropertyChanged += TeamExplorerOnPropertyChanged;
				eventRegistered = true;
			}			
		}

		private async void TeamExplorerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{			
			if (TeamExplorer != null && e.PropertyName == CoreExtensions.GetMemberName(() => TeamExplorer.CurrentPage))
			{
				TeamExplorer.PropertyChanged -= TeamExplorerOnPropertyChanged;				
				eventRegistered = false;
				await RefreshAsync();
			}
		}
		
		private Changeset GetChangesetFromCurrentPage()
		{
			return TeamExplorer.CurrentPage.FindChangesetFromTeamExplorerPage();
		}

		private Func<IBuildDetail, bool> GetFilter()
		{
			if (UserContext != null)
				return detail => detail.RequestedFor == UserContext.UserName;

			Changeset changeset = GetChangesetFromCurrentPage();
			if (changeset != null)
			{
				return detail =>
				{
					var chString = detail.SourceGetVersion.Replace("C", "");
					return (changeset.ChangesetId.ToString() == chString) || (InformationNodeConverters.GetAssociatedChangesets(detail).Any(summary => summary.ChangesetId == changeset.ChangesetId));
				};
			}
			return detail => true;
		}

		private void SetTitle(string s)
		{
			title = s;
			RaisePropertyChanged(() => Title);
		}

		private async Task RefreshAsync(bool newUser = false)
		{
			if (!IsEnabled || TfsContext == null)
			{
				IsBusy = false;
				return;
			}
			try
			{
				if (UserContext == null && !TfsContext.BuildDetailManager.IsBackgroundBuildDefinitionLoadingEnabled)
				{
					IsVisible = false;
					return;
				}
				IsBusy = true;
				SetTitle("Last Triggered Builds");
				if (UserContext == null)
					await Task.Delay(1000);

				if (TfsContext.BuildDetailManager.AllBuildDetailsLoaded || UserContext == null)
				{
                    
                    var pageView = TeamExplorer.CurrentPage.PageContent as BuildsPageView;
					BuildDetails = pageView != null && !newUser && !IsInUserInfoPage
						? new ObservableCollection<IBuildDetail>((await TfsContext.BuildDetailManager.QueryLastBuildsAsync()))
						: new ObservableCollection<IBuildDetail>((await TfsContext.BuildDetailManager.QueryBuildsAsync()).Where(GetFilter()));
					SetTitle(string.Format("Last {0} Builds {1}", BuildDetails.Count, pageView == null ? "for this Changeset" :string.Empty));
				}
				else
				{
					BuildDetails = new ObservableCollection<IBuildDetail>((await TfsContext.BuildDetailManager.QueryBuildsForUserAsync(UserContext.UserName)));
				}

				if (UserContext != null)
					SetTitle(string.Format("Last {0} Builds for {1}", BuildDetails.Count, UserContext.UserName));

				IsVisible = BuildDetails.Any();
				if (TfsContext.BuildDetailManager.IsBackgroundBuildDefinitionLoadingEnabled)
				{
					foreach (var buildDetail in BuildDetails)
						buildDetail.StatusChanged += (sender, args) => RaisePropertyChanged(() => BuildDetails);
				}
			}
			finally
			{
				IsBusy = false;
				RaisePropertiesChanged(() => HasItems, () => IsInUserInfoPage, () => IsUserNameVisible, () => BuildDetails);
				UserInfoCommand.RaiseCanExecuteChanged();
			}
		}

	}
}