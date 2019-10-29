using System;
using System.ComponentModel.Composition;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationLinks
{
    [TeamExplorerNavigationLink(GuidList.recentChangesLink, TeamExplorerNavigationItemIds.PendingChanges, 200)]
    public class RecentChangesNavigationLink : SimplePageNavigationLink
    {		
		public override Guid PageIdToNavigate
	    {
			get { return GuidList.recentChangesPage.ToGuid(); }
	    }

	    public override string Text
	    {
		    get { return "Recent Changes"; }
	    }

        [ImportingConstructor]
		protected RecentChangesNavigationLink([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
			:base(serviceProvider)
	    {}
    }
}
