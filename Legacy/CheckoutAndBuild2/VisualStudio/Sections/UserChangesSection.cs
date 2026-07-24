using System;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
    /// <summary>
    /// Changes section.
    /// </summary>
	[TeamExplorerSection(GuidList.userChangesSectionId, GuidList.userInfoPage, 10)]
	public class UserChangesSection : ChangesSectionBase, IUserContextSection
    {

	    public UserInfoContext UserContext { get; set; }

	    /// <summary>
	    /// Initialize override.
	    /// </summary>
	    public override void Initialize(object sender, IServiceProvider provider, object context)
	    {
		    base.Initialize(sender, provider, context);
		    UserInfoContext infoContext = context as UserInfoContext;
		    if(infoContext != null)
				UserContext = infoContext;
	    }

	   
	    /// <summary>
        /// Get the parameters for the history query.
        /// </summary>
        protected override void GetHistoryParameters(VersionControlServer vcs, out string user, out int maxCount)
        {
            user = UserContext.UserName;
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
		    get { return string.Format("{0} Changes", UserContext.UserName); }
	    }
    }
}
