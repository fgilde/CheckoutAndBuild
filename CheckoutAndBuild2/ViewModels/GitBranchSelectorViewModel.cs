using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using Microsoft.TeamFoundation.VersionControl.Client;
using ContextMenu = System.Windows.Controls.ContextMenu;

namespace FG.CheckoutAndBuild2.ViewModels
{
    public class GitBranchSelectorViewModel : BaseViewModel
    {

        private ContextMenu _selectBranchMenu;
        private bool isPrivateSet = false;
        private string selectedBranch;
        private FileSystemWatcher branchWatcher;


        public GitBranchSelectorViewModel(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            TfsContext.SelectedWorkspaceChanged += TfsContextOnSelectedWorkspaceChanged;
        }

        private void TfsContextOnSelectedWorkspaceChanged(object sender, EventArgs<Workspace> eventArgs)
        {
            if (!isPrivateSet)
            {
                UpdateWatcher();
                SelectedBranch = string.Empty;
                if (IsAvailable)
                    SelectedBranch = GitHelper.GetCurrentBranch(TfsContext?.SelectedDirectory);
                RaiseAllPropertiesChanged();
            }
        }
        public bool IsAvailable => Settings.Get(SettingsKeys.GitSettingsPerBranch, true) && !string.IsNullOrEmpty(TfsContext?.SelectedDirectory) && GitHelper.IsGitControlled(TfsContext.SelectedDirectory) && Branches.Any();

        public ObservableCollection<string> Branches => new ObservableCollection<string>(GitHelper.GetBranches(TfsContext?.SelectedDirectory));

        public ContextMenu SelectBranchMenu => Branches.Select(branch => new DelegateCommand<object>(branch, o => SelectedBranch = branch)).ToContextMenu();

        public string SelectedBranch
        {
            get => selectedBranch ?? (selectedBranch = !string.IsNullOrEmpty(TfsContext?.SelectedDirectory) ? GitHelper.GetCurrentBranch(TfsContext?.SelectedDirectory) : "");
            set
            {
                using (new PauseCheckedActionScope(() => isPrivateSet = true, () => isPrivateSet = false))
                {
                    if(TfsContext != null)
                        TfsContext.SelectedGitBranch = value;
                    if (SetProperty(ref selectedBranch, value))
                    {
                        SyncBranchIf();
                    }
                }
            }
        }

        private void SyncBranchIf()
        {
            if (Settings.Get(SettingsKeys.SyncGitBranch, true))
            {
                if(branchWatcher != null)
                    branchWatcher.EnableRaisingEvents = false;
                selectedBranch = GitHelper.SetCurrentBranch(TfsContext?.SelectedDirectory, selectedBranch);
                if (branchWatcher != null)
                    branchWatcher.EnableRaisingEvents = true;
            }
        }

        protected override void OnUpdateSynchronous(CancellationToken cancellationToken)
        {
            UpdateWatcher();
            RaiseAllPropertiesChanged();
        }

        private void UpdateWatcher()
        {
            UnsetCurrentWatcher();
            if (Settings.Get(SettingsKeys.SyncGitBranch, true))
            {
                //GetService<ISettingsService>()
                string dir = TfsContext.SelectedDirectory;
                var context = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();
                var invoker = new Action(() =>
                {
                    SelectedBranch = GitHelper.GetCurrentBranchName(dir);
                });
                var headFile = GitHelper.GetRepoHeadFile(dir);
                if (headFile != null)
                {
                    branchWatcher = new FileSystemWatcher(headFile.DirectoryName, headFile.Name);
                    branchWatcher.Changed += (sender, args) =>
                    {
                        Task.Delay(1).ContinueWith(task => invoker(), context);
                    };
                    branchWatcher.EnableRaisingEvents = true;
                }
            }
        }

        private void UnsetCurrentWatcher()
        {
            if (branchWatcher != null)
            {
                branchWatcher.EnableRaisingEvents = false;
                branchWatcher.Dispose();
                branchWatcher = null;
            }
        }
    }
}