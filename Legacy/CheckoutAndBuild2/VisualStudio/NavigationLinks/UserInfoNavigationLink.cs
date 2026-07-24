using System;
using System.ComponentModel.Composition;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationLinks
{
	[TeamExplorerNavigationLink(GuidList.userInfoNavigationLink, TeamExplorerNavigationItemIds.MyWork, 200)]
	public class UserInfoNavigationLink : SimplePageNavigationLink
	{
		public override Guid PageIdToNavigate => GuidList.userInfoPage.ToGuid();

	    public override string Text => "Personal User Info";

	    /// <summary>
		/// Execute this link.
		/// </summary>
		public override void Execute()
		{
			if(Context == null)
				InitUserContext();
			base.Execute();
		}

		[ImportingConstructor]
		protected UserInfoNavigationLink([Import(typeof (SVsServiceProvider))] IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
			InitUserContext();
		}

		private void InitUserContext()
		{
			if (TfsContext != null && TfsContext.VersionControlServer != null &&
				TfsContext.VersionControlServer.AuthorizedIdentity != null)
				Context = new UserInfoContext(TfsContext.VersionControlServer?.AuthorizedIdentity);
		}
	}
}
