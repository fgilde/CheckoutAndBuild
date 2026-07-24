using System;
using System.Linq;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    /// <summary>
    /// Recent changes page.
    /// </summary>
	[TeamExplorerPage(GuidList.historyPage, Undockable = true, MultiInstances = true)]
    public class HistoryPage : TeamExplorerBase
    {
	    private string title;
        public override string Title => title;

        public override object Content => new RecentChangesPageView {DataContext = this};

        public override void Initialize(object sender, IServiceProvider provider, object context)
	    {
		    base.Initialize(sender, provider, context);
		    if (context is string s)
		    {
			    title = "History for " + s;
			    foreach (var section in this.GetSections().OfType<ChangesSectionBase>())
				    section.QueryPath = s;
		    }
	    }

    }
}
