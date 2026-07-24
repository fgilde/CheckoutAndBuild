using System;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
    /// <summary>
    /// Changes section.
    /// </summary>
	[TeamExplorerSection(GuidList.myChangesSectionId, GuidList.recentChangesPage, 10)]
    public class MyChangesSection : ChangesSectionBase
    {

	    /// <summary>
        /// Get the parameters for the history query.
        /// </summary>
        protected override void GetHistoryParameters(VersionControlServer vcs, out string user, out int maxCount)
        {
            user = vcs.AuthorizedUser;
            maxCount = 10;
        }

	    /// <summary>
	    /// Get the title of this page. If the title changes, the PropertyChanged event should be raised.
	    /// </summary>
	    /// <returns>
	    /// Returns <see cref="T:System.String"/>.
	    /// </returns>
	    public override string Title => "My Changes";
    }
}
