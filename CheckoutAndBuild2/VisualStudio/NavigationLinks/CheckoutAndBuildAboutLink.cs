using System;
using System.ComponentModel.Composition;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationLinks
{
	[TeamExplorerNavigationLink(GuidList.checkoutAndBuildAboutLinkId, GuidList.checkoutAndBuildTeamExplorerNavigationItem, 500)]
	public class CheckoutAndBuildAboutLink : SimplePageNavigationLink
	{
		public override string Text { get { return string.Format("About"); } }

		public override Guid PageIdToNavigate { get { return GuidList.aboutPageId.ToGuid(); } }

		[ImportingConstructor]
		public CheckoutAndBuildAboutLink([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) : 
			base(serviceProvider)	
		{}
	}
}