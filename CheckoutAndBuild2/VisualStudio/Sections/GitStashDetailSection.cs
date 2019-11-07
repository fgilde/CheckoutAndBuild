using System;
using System.Linq;
using System.Windows.Controls;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
    /// <summary>
    /// Changes section base class.
    /// </summary>
    [TeamExplorerSection(GuidList.gitStashDetailSection, GuidList.gitStashPage, 300)]
    public class GitStashDetailSection : TeamExplorerBase
    {
        private GitStashInfo stashInfo;
        private object extraContent;
        public override string Title => $"Stash Details {StashInfo?.Stash?.Name} ({StashInfo?.Stash?.Id})";

        public override object Content => this;

        public bool HasItems => StashInfo != null && StashInfo.Changes.Any();

        public ContextMenu ContextMenu { get; }

        public UserInfoContext UserContext { get; set; }

        public GitStashInfo StashInfo
        {
            get => stashInfo;
            set
            {
                if (SetProperty(ref stashInfo, value))
                {
                    ApplyStashCommand.RaiseCanExecuteChanged();
                    DeleteStashCommand.RaiseCanExecuteChanged();
                    CreateBranchForStashCommand.RaiseCanExecuteChanged();
                }
            }

        }


        public object ExtraContent
        {
            get => extraContent;
            set => SetProperty(ref extraContent, value);
        }

        
        public DelegateCommand<GitStashInfo> ApplyStashCommand { get; }
        public DelegateCommand<GitStashInfo> DeleteStashCommand { get; }
        public DelegateCommand<GitStashInfo> CreateBranchForStashCommand { get; }

        public GitStashDetailSection()
        {            
            ApplyStashCommand = new DelegateCommand<GitStashInfo>("Apply selected stash", ApplySelectedStash, CanApplySelectedStash) { IconImage = Images.WorkItem_16xLG.ToImageSource() };
            CreateBranchForStashCommand = new DelegateCommand<GitStashInfo>("Create branch for", CreateBranchForStash, CanApplySelectedStash) { IconImage = Images.Branch___01.ToImageSource() };
            DeleteStashCommand = new DelegateCommand<GitStashInfo>("Delete", DeleteSelectedStash, CanApplySelectedStash) { IconImage = Images.FolderOffline_7441.ToImageSource() };
            ContextMenu = new IUICommand[] { CreateBranchForStashCommand, DeleteStashCommand }.ToContextMenu();
        }

        private void CreateBranchForStash(GitStashInfo stash)
        {
            stash = stash ?? StashInfo;
            var section = GitStashsSection();
            section?.CreateBranchForStashCommand.Execute(stash.Stash);
        }

        private void DeleteSelectedStash(GitStashInfo stash)
        {
            stash = stash ?? StashInfo;
            var section = GitStashsSection();
            section?.DeleteSelectedStashCommand.Execute(stash.Stash);
        }

        private GitStashsSection GitStashsSection()
        {
            var section = TeamExplorer.CurrentPage.GetSections().OfType<GitStashsSection>().FirstOrDefault();
            if (section != null)
                section.IsExpanded = true;
            return section;
        }

        private bool CanApplySelectedStash(GitStashInfo arg)
        {
            return arg != null || StashInfo != null;
        }

        private void ApplySelectedStash(GitStashInfo stash)
        {
            stash = stash ?? StashInfo;
            GitHelper.ApplyStash(stash.Stash);
            Output.Notification($"Successfully applied stash {stash.Stash.Name}");
            RefreshPage();
        }

        private void RefreshPage()
        {
            TeamExplorer.CurrentPage.Refresh();
            Refresh();
        }
        
        public override void Initialize(object sender, IServiceProvider provider, object context)
        {
            base.Initialize(sender, provider, context);
            if (UserContext == null)
                UserContext = new UserInfoContext(TfsContext.VersionControlServer?.AuthorizedIdentity);
            StashInfo = StashInfo ?? context as GitStashInfo;
            IsVisible = TfsContext.IsGitConnected && StashInfo != null;
        }

    }
}
