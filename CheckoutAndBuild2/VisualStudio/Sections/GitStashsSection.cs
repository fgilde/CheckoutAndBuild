using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
    /// <summary>
    /// Changes section base class.
    /// </summary>
    [TeamExplorerSection(GuidList.checkoutAndBuildGitStashesSection, TeamExplorerPageIds.GitChanges, 200)]
    [TeamExplorerSectionPlacement(GuidList.gitStashPage, 200)]
    public class GitStashsSection : TeamExplorerBase
    {
        private GitStash selectedStash;
        private object extraContent;
        public override string Title => "Stashes";

        public override object Content => this;

        public bool HasItems => Stashs != null && Stashs.Any();
        
        public ContextMenu ContextMenu { get; }

        public UserInfoContext UserContext { get; set; }


        public IEnumerable<IUICommand> WorkItemContextMenuCommands
        {
            get
            {
                yield return new DelegateCommand<object>("Change Query", ChangeWorkItemsQuery);
                yield return new DelegateCommand<object>("Enter WorkitemID", EnterWorkItemId);
                yield return new SeparatorCommand();
            }
        }

        public object ExtraContent
        {
            get => extraContent;
            set => SetProperty(ref extraContent, value);
        }

        public GitStash SelectedStash
        {
            get => selectedStash;
            set
            {
                if (SetProperty(ref selectedStash, value))
                {
                    ApplySelectedStashCommand.RaiseCanExecuteChanged();
                    ShowStashDetailsCommand.RaiseCanExecuteChanged();
                    DeleteSelectedStashCommand.RaiseCanExecuteChanged();
                    CreateBranchForStashCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand<string> CreateStashCommand { get; }
        public DelegateCommand<GitStash> ApplySelectedStashCommand { get; }
        public DelegateCommand<GitStash> DeleteSelectedStashCommand { get; }
        public DelegateCommand<GitStash> ShowStashDetailsCommand { get; }
        public DelegateCommand<GitStash> CreateBranchForStashCommand { get; }

        public GitStashsSection()
        {
            CreateStashCommand = new DelegateCommand<string>("Create new stash...", CreateNewStash, CanCreateStash) { IconImage = Images.NewSolutionFolder_6289.ToImageSource() };
            ApplySelectedStashCommand = new DelegateCommand<GitStash>("Apply selected stash", ApplySelectedStash, CanApplySelectedStash) { IconImage = Images.WorkItem_16xLG.ToImageSource() };
            ShowStashDetailsCommand = new DelegateCommand<GitStash>("Show Details", ShowStashDetails, CanApplySelectedStash) { IconImage = Images.ResultstoGrid_9947.ToImageSource() };
            CreateBranchForStashCommand = new DelegateCommand<GitStash>("Create branch for", CreateBranchForStash, CanApplySelectedStash) { IconImage = Images.Branch___01.ToImageSource() };
            DeleteSelectedStashCommand = new DelegateCommand<GitStash>("Delete", DeleteSelectedStash, CanApplySelectedStash) {IconImage = Images.FolderOffline_7441.ToImageSource()};            
            ContextMenu = new IUICommand[] { CreateStashCommand, ApplySelectedStashCommand, ShowStashDetailsCommand, CreateBranchForStashCommand, DeleteSelectedStashCommand }.ToContextMenu();
        }

        private async void CreateBranchForStash(GitStash stash)
        {
            stash = stash ?? SelectedStash;
            
            var nameEdit = new NameEdit((ne, value) =>
            {                
                GitHelper.CreateBranchForStash(stash, value);                
                SelectedStash = null;
                ExtraContent = null;
                TeamExplorer.NavigateToPage(TeamExplorerPageIds.GitBranches.ToGuid(), null);
            }, ne =>
            {                
                ExtraContent = null;
            })
            {                                
                AcceptText = "Create",
                ExtraCommandsMenu = await GetWorkItemContextMenu()
            };
            ExtraContent = nameEdit;            
        }

        private void DeleteSelectedStash(GitStash stash)
        {
            stash = stash ?? SelectedStash;

            IsEnabled = false;
            var nameEdit = new NameEdit((ne, value) =>
            {
                IsEnabled = true;
                GitHelper.DeleteStash(stash);
                Stashs.Remove(SelectedStash);
                SelectedStash = null;
                ExtraContent = null;
                RefreshPage();
            }, ne =>
            {
                IsEnabled = true;
                ExtraContent = null;
            })
            {
                IsReadOnly = true,
                Value = $"Stash '{stash.Name} ({stash.Id})' will be deleted!",
                AcceptText = "Delete"
            };
            ExtraContent = nameEdit;            
        }

        private async Task<IEnumerable<WorkItem>> GetWorkItems()
        {
            var query = serviceProvider.Get<SettingsService>().Get(SettingsKeys.WorkItemSectionQueryKey(UserContext), TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity));
            if (string.IsNullOrEmpty(query))
                query = TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity);
            return (await TfsContext.WorkItemManager.RunQueryAsync(query)).OfType<WorkItem>();
        }

        private async Task<ContextMenu> GetWorkItemContextMenu()
        {
            var commands = WorkItemContextMenuCommands.Concat((await GetWorkItems()).Select(item => new DelegateCommand<WorkItem>($"{item.Id} - ({item.Type.Name}) {item.Title}", MakeNameSuggestionForWorkItem)
            {
                Parameter = item
            }));
            return commands.ToContextMenu();
        }

        private void ChangeWorkItemsQuery(object obj)
        {
            var query = serviceProvider.Get<SettingsService>().Get(SettingsKeys.WorkItemSectionQueryKey(UserContext), TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity));
            if (string.IsNullOrEmpty(query))
                query = TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity);
            QueryEditorDialog dialog = new QueryEditorDialog(query) { CanSelectQuery = true };
            dialog.RunQuery += (o, args) => TfsContext.WorkItemManager.ShowQueryResults(WorkItemManager.PrepareQueryText(dialog.Query));
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                serviceProvider.Get<SettingsService>().Set(SettingsKeys.WorkItemSectionQueryKey(UserContext), dialog.Query);
        }

        private void EnterWorkItemId(object obj)
        {
            if (int.TryParse(Prompt.ShowDialog("Enter WorkitemId", "Workitem Id"), out int id))
            {
                var workItem = Check.TryCatch<WorkItem, Exception>(() => TfsContext.WorkItemManager.WorkItemStore.GetWorkItem(id));
                if (workItem != null)
                    MakeNameSuggestionForWorkItem(workItem);
                else
                    MessageBox.Show($"No workitem with Id ({id}) found");
            }
        }

        private void MakeNameSuggestionForWorkItem(WorkItem workItem)
        {
            if (ExtraContent is NameEdit e)
                e.Value = ExtendedGitBranchesSection.GetNameSuggestion(workItem, TfsContext.SelectedDirectory);
        }

        private void ShowStashDetails(GitStash stash)
        {
            stash = stash ?? SelectedStash;
            var details = GitHelper.GetStashDetails(stash);
            TeamExplorer.NavigateToPage(GuidList.gitStashPage.ToGuid(), details);            
        }

        private bool CanApplySelectedStash(GitStash arg)
        {
            return arg != null || SelectedStash != null;
        }

        private void ApplySelectedStash(GitStash stash)
        {
            stash = stash ?? SelectedStash;
            GitHelper.ApplyStash(stash);
            Output.Notification($"Successfully applied stash {stash.Name}");
            RefreshPage();
        }

        private bool CanCreateStash(object o)
        {
            return true; //GitHelper.HasChanges(TfsContext.SelectedDirectory);
        }

        private void CreateNewStash(string stashName)
        {
            if (string.IsNullOrEmpty(stashName))
            {
                var nameEdit = new NameEdit((ne, value) => CreateNewStash(value), ne => ExtraContent = null)
                {
                    AcceptText = "Create"
                };
                ExtraContent = nameEdit;
            }
            else
            {
                var stash = GitHelper.CreateStash(TfsContext.SelectedDirectory, stashName);
                if (stash != null)
                {
                    ExtraContent = null;
                    Stashs.Insert(0, stash);
                    Output.Notification($"Successfully created stash {stashName}");
                }
                else
                    Output.NotificationError($"Failed to create stash {stashName}");
            }
        }

        private void RefreshPage()
        {
            TeamExplorer.CurrentPage.Refresh();
            Refresh();
        }

        public ObservableCollection<GitStash> Stashs { get; set; }

        public override void Initialize(object sender, IServiceProvider provider, object context)
        {
            base.Initialize(sender, provider, context);
            if (UserContext == null)
                UserContext = new UserInfoContext(TfsContext.VersionControlServer.AuthorizedIdentity);
            IsVisible = TfsContext.IsGitConnected;
            Load();
        }

        private void Load()
        {            
            Stashs = new ObservableCollection<GitStash>(GitHelper.GetStashes(TfsContext.SelectedDirectory));
            CreateStashCommand.RaiseCanExecuteChanged();
        }
        
        public override void Refresh()
        {
            base.Refresh();
            Load();
        }
    }
}
