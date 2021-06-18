using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.shelveSetSectionId, GuidList.userInfoPage, 1100)]	
	[TeamExplorerSectionPlacement(TeamExplorerPageIds.MyWork, 1100)]
	public class ShelvesetSection : TeamExplorerBase, IUserContextSection
	{
		
		private bool isInUserInfoPage;
		private ObservableCollection<Shelveset> shelvesets;

		/// <summary>
		/// Get the title of this page. If the title changes, the PropertyChanged event should be raised.
		/// </summary>
		/// <returns>
		/// Returns <see cref="T:System.String"/>.
		/// </returns>
		public override string Title
		{
			get { return string.Format("Shelvesets for {0}", UserContext.UserName); }
		}
		
		public ObservableCollection<Shelveset> Shelvesets
		{
			get { return shelvesets; }
			set { SetProperty(ref shelvesets, value); }
		}

		private Shelveset selectedShelveset;

		public Shelveset SelectedShelveset
		{
			get { return selectedShelveset; }
			set
			{
				if(SetProperty(ref selectedShelveset, value))
					VisualStudioTrackingSelection.UpdateSelectionTracking(SelectedShelveset);
			}
		}

		public bool IsInUserInfoPage
		{
			get { return isInUserInfoPage; }
			set { SetProperty(ref isInUserInfoPage, value); }
		}

		public override object Content
		{
			get { return this; }
		}

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
			if (UserContext == null && TfsContext.VersionControlServer != null)
				UserContext = new UserInfoContext(TfsContext.VersionControlServer.AuthorizedIdentity);
			else
				IsInUserInfoPage = true;

			TeamExplorer.PropertyChanged -= TeamExplorerOnPropertyChanged;
			TeamExplorer.PropertyChanged += TeamExplorerOnPropertyChanged;

			await RefreshAsync();
		}

		public bool HasItems { get { return Shelvesets != null && Shelvesets.Any(); } }

		private void TeamExplorerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (TeamExplorer != null && e.PropertyName == CoreExtensions.GetMemberName(() => TeamExplorer.CurrentPage))
			{
				IsInUserInfoPage = TeamExplorer.CurrentPage != null && TeamExplorer.CurrentPage.GetType() == typeof(UserInfoPage);
			}
		}

		private async Task RefreshAsync()
		{
			if (!IsEnabled)
			{
				IsBusy = false;
				return;
			}
			try
			{				
				IsBusy = true;
				Shelvesets = new ObservableCollection<Shelveset>(await Task.Run(() => TfsContext.VersionControlServer.QueryShelvesets(null, UserContext.UserName)));				
				IsVisible = Shelvesets.Any();
			}
			finally
			{
				IsBusy = false;
				RaisePropertyChanged(() => HasItems);
			}
		}


	}
}