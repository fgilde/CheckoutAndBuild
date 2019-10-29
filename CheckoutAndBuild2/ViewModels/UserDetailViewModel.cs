using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Framework.Client;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class UserDetailViewModel : BaseViewModel
	{
		private ImageSource image;
		private string displayName;
		private string email;
		private string uniqueName;
		private TeamFoundationIdentity identity;
		private ObservableCollection<TeamFoundationIdentity> allUsers;

		public UserDetailViewModel(TeamFoundationIdentity identity, IServiceProvider serviceProvider) 
			: base(serviceProvider)
		{			
			this.identity = identity;
			FillModelData();
			if (this.identity != null && !AllUsers.Contains(this.identity))
				this.identity = AllUsers.FirstOrDefault(i => i.TeamFoundationId == this.identity.TeamFoundationId) ?? identity;
		}

		public TeamFoundationIdentity Identity
		{
			get { return identity; }
			set
			{
				if (SetProperty(ref identity, value) && identity != null)
					UpdateUser();
			}
		}

		private void UpdateUser()
		{
			var userInfoPage = TeamExplorer.CurrentPage as UserInfoPage;
			if (userInfoPage != null)
				TeamExplorer.NavigateToPage(TeamExplorerPageIds.Home.ToGuid(), null);
			TeamExplorer.NavigateToPage(GuidList.userInfoPage.ToGuid(), new UserInfoContext(Identity));
		}

		public string UniqueName
		{
			get { return uniqueName; }
			set { SetProperty(ref uniqueName, value); }
		}

		public string Email
		{
			get { return email; }
			set { SetProperty(ref email, value); }
		}

		public string DisplayName
		{
			get { return displayName; }
			set { SetProperty(ref displayName, value); }
		}

		public ImageSource Image
		{
			get { return image; }
			set { SetProperty(ref image, value); }
		}

		public ObservableCollection<TeamFoundationIdentity> AllUsers
		{
			get { return allUsers; }
			set { SetProperty(ref allUsers, value); }
		} 

		private async void FillModelData()
		{
			if (identity != null)
			{
				DisplayName = identity.DisplayName;
				Email = Check.TryCatch<string, Exception>(() => identity.GetProperty("Mail").ToString());
				UniqueName = identity.UniqueName;
				AllUsers =
					new ObservableCollection<TeamFoundationIdentity>(
						TfsContext.IdentityManager.GetAllIdentities().Where(i => !i.DisplayName.Contains(@"]\")));

				var _image = await TfsContext.IdentityManager.GetImageAsync(identity);
				if (_image != null)
					Image = new Bitmap(_image).ToImageSource();
			}
		}
	}
}