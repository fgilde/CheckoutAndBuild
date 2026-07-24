using System;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
    /// <summary>
    /// All Changes section.
    /// </summary>
	[TeamExplorerSection(GuidList.allChangesSectionId, GuidList.recentChangesPage, 20)]
	[TeamExplorerSectionPlacement(GuidList.historyPage, 20)]
	public class AllChangesSection : ChangesSectionBase
    {


        /// <summary>
        /// Get the parameters for the history query.
        /// </summary>
        protected override void GetHistoryParameters(VersionControlServer vcs, out string user, out int maxCount)
        {
            user = null;
            maxCount = 100;
        }

	    /// <summary>
	    /// Get the title of this page. If the title changes, the PropertyChanged event should be raised.
	    /// </summary>
	    /// <returns>
	    /// Returns <see cref="T:System.String"/>.
	    /// </returns>
	    public override string Title
	    {
		    get { return "All Changes"; }
	    }
    }
}
