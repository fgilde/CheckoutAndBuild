using System;
using FG.CheckoutAndBuild2.Controls;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    /// <summary>
    /// Recent changes page.
    /// </summary>
    [TeamExplorerPage(GuidList.recentChangesPage, Undockable = true, MultiInstances = false)]
    public class RecentChangesPage : TeamExplorerBase
    {

	    public override object Content
	    {
		    get { return new RecentChangesPageView {DataContext = this}; }
	    }

	    /// <summary>
	    /// Get the title of this page. If the title changes, the PropertyChanged event should be raised.
	    /// </summary>
	    /// <returns>
	    /// Returns <see cref="T:System.String"/>.
	    /// </returns>
	    public override string Title
	    {
		    get { return "Recent Changes"; }
	    }
    }
}
