using System;
using System.ComponentModel.Composition;
using System.Drawing;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio.NavigationLinks;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationItems
{
	[TeamExplorerNavigationItem(GuidList.userPageTeamExplorerNavigationItem, 100, TargetPageId = "1F9974CD-16C3-4AEF-AED2-0CE37988E2F1")]
	public class UserPageNavigationItem : NotificationObject, ITeamExplorerNavigationItem
	{
        private readonly IServiceProvider serviceProvider;
		private Image image = Images.user_32xLG;
		private bool isVisible = true;
		private string text = "User Information";
	    private UserInfoContext user = null;
	    private readonly SettingsService settingsService;

	    [ImportingConstructor]
		public UserPageNavigationItem([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
		{
			this.serviceProvider = serviceProvider;
			TfsContext = serviceProvider.Get<TfsContext>();
			settingsService = serviceProvider.Get<SettingsService>();            
		}

		public TfsContext TfsContext { get;  }

		public Image Image
		{
			get { return image; }
			set { SetProperty(ref image, value); }
		}
		
		public bool IsVisible
		{
			get { return isVisible; }
			set { SetProperty(ref isVisible, value); }
		}
		
		public string Text
		{
			get { return text; }
			set { SetProperty(ref text, value); }
		}

		public void Execute()
		{
            if (user != null)
                serviceProvider?.Get<ITeamExplorer>().NavigateToPage(GuidList.userInfoPage.ToGuid(), user);
        }

	    public async void Invalidate()
	    {
	        if (settingsService != null && settingsService.Get(SettingsKeys.ShowUserInfoLinkKey, true) &&
	            TfsContext?.VersionControlServer?.AuthorizedIdentity != null)
	        {
	            user = new UserInfoContext(TfsContext.VersionControlServer?.AuthorizedIdentity);
	            Text = $"Info {user.UserName}";
	            IsVisible = true;
	            var _image = await TfsContext.IdentityManager.GetImageAsync(user.Identity);
	            Image = _image != null ? new Bitmap(_image) : Images.user_32xLG;
	        }
	        else
	            IsVisible = false;
	    }

		public void Dispose()
		{}

	}

    [TeamExplorerNavigationLink(GuidList.disableUserInfoNavigationItemLink, GuidList.userPageTeamExplorerNavigationItem, 200)]
    public class DisableUserInfoNavigationItemLink : SimplePageNavigationLink
    {
        public override string Text => "Don't show this Item";

        /// <summary>
        /// Execute this link.
        /// </summary>
        public override void Execute()
        {
            Settings.Set(SettingsKeys.ShowUserInfoLinkKey, false);
            TeamExplorer?.CurrentPage?.Refresh();
        }

        [ImportingConstructor]
        protected DisableUserInfoNavigationItemLink([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
            : base(serviceProvider)
        {}
      
    }
}