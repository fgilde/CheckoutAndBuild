using System;
using System.Linq;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Common.Internal;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    /// <summary>
    /// Recent changes page.
    /// </summary>
	[TeamExplorerPage(GuidList.gitStashPage, Undockable = true, MultiInstances = true)]
    public class GitStashPage : TeamExplorerBase
    {
        public override string Title { get; } = "Git Stashes";

        public override object Content => new RecentChangesPageView {DataContext = this};

        public override void Initialize(object sender, IServiceProvider provider, object context)
	    {            
		    base.Initialize(sender, provider, context);
	        if (context is GitStashInfo si)
	        {
	            SetStash(si.Stash);
                SetStashInfo(si);
	        }
	        else if (context is GitStash s)
	        {
	            SetStash(s);
	            SetStashInfo(GitHelper.GetStashDetails(s));
	        }
	    }

        private void SetStashInfo(GitStashInfo gitStashInfo)
        {
            foreach (var section in this.GetSections().OfType<GitStashDetailSection>())
            {
                section.StashInfo = gitStashInfo;
                section.IsVisible = gitStashInfo != null;
            }
        }

        private void SetStash(GitStash s)
        {
            foreach (var section in this.GetSections().OfType<GitStashsSection>())
            {
                section.SelectedStash = s;
                section.IsExpanded = false;
            }
        }
    }
}
