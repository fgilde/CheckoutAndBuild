using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Git.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;
using ContextMenu = System.Windows.Controls.ContextMenu;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class WorkspaceSelectorViewModel : BaseViewModel
	{
		private Workspace selectedWorkspace;
		private ContextMenu selectWorkspaceMenu;
		private ObservableCollection<Workspace> workspaces;
		private bool isPrivateSet = false;
	    private System.Threading.Tasks.TaskScheduler taskSchedulerContext;


        public IUICommand SelectDirectoryCommand { get; }

		public WorkspaceSelectorViewModel(IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
            taskSchedulerContext = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();
            Workspaces = new ObservableCollection<Workspace>();
			TfsContext.SelectedWorkspaceChanged += TfsContextOnSelectedWorkspaceChanged;
			SelectDirectoryCommand = new DelegateCommand<object>("Select a directory...", SelectDirectory) { IconImage = Properties.Images.folder_Open_16xLG.ToImageSource() };
		}

		private void TfsContextOnSelectedWorkspaceChanged(object sender, EventArgs<Workspace> eventArgs)
		{
			if (!isPrivateSet)
			{
				SelectedWorkspace = eventArgs.Value;
			}
		}

		public ObservableCollection<Workspace> Workspaces
		{
			get { return workspaces; }
			set { SetProperty(ref workspaces, value); }
		}

		public ContextMenu SelectWorkspaceMenu
		{
			get { return selectWorkspaceMenu; }
			set { SetProperty(ref selectWorkspaceMenu, value); }
		}

		public Workspace SelectedWorkspace
		{
			get { return selectedWorkspace; }
			set
			{
				using (new PauseCheckedActionScope(() => isPrivateSet = true, () => isPrivateSet = false))
				{
					TfsContext.SelectedWorkspace = value;
					SetProperty(ref selectedWorkspace, value);
					RaisePropertiesChanged(() => SelectedWorkspaceName, () => ToolTip);
					Settings.Set(SettingsKeys.LastWorkspaceKey, SelectedWorkspace != null ? SelectedWorkspace.Name : string.Empty);
				}
			}
		}

		public string SelectedWorkspaceName => SelectedWorkspace != null ? SelectedWorkspace.Name :
		    (TfsContext != null && TfsContext.IsLocalConnected ? GetDisplayNameForLocalDirectory() : "Select Workspace");

	    public string ToolTip
		{
			get
			{
				if (SelectedWorkspace != null)
					return SelectedWorkspace.DisplayName;
				if (TfsContext.IsLocalConnected)
					return TfsContext.SelectedDirectory;
				return string.Empty;
			}
		}

		private string GetDisplayNameForLocalDirectory()
		{
			var repo = TfsContext.CurrentGitRepository;
			if (repo != null)
				return $"{repo.Name}";
			return FileHelper.ToShortPath(TfsContext.SelectedDirectory, 25);
		}
        
		protected override async void OnUpdateSynchronous(CancellationToken cancellationToken)
		{
		    if (!cancellationToken.IsCancellationRequested)
		    {
		        if (TfsContext.VersionControlServer != null)
		        {
		            TfsContext.VersionControlServer.CreatedWorkspace -= VersionControlServerOnWorkspacesChanged;
		            TfsContext.VersionControlServer.DeletedWorkspace -= VersionControlServerOnWorkspacesChanged;
		            TfsContext.VersionControlServer.CreatedWorkspace += VersionControlServerOnWorkspacesChanged;
		            TfsContext.VersionControlServer.DeletedWorkspace += VersionControlServerOnWorkspacesChanged;
		        }

		        await LoadWorkspacesAsync(cancellationToken);
		    }
            BuildContextMenu();
            TryLoadLastWorkspace();
        }

	    public void BuildContextMenu()
	    {
	        SelectWorkspaceMenu =
	            Workspaces.Select(workspace => new DelegateCommand<object>(workspace.Name, o => SelectedWorkspace = workspace))
	                .Concat(GetGitRepositoriesCommands())
	                .Concat(StaticCommands.Merge(Workspaces != null && Workspaces.Any() ? StaticCommands.Seperator : null,
	                    SelectDirectoryCommand,
	                    StaticCommands.Seperator,
	                    TfsContext.IsTfsConnected
	                        ? StaticCommands.ManageWorkspacesCommand
	                        : StaticCommands.ConnectToSourceControlCommand))
	                .ToContextMenu();
	    }

	    private IEnumerable<IUICommand> GetGitRepositoriesCommands()
	    {
	        bool allowGitAndTFSWorkspaces = true;
			var tfs = TfsContext;
			if (!tfs.IsTfsConnected || allowGitAndTFSWorkspaces) 
			{
				//GitRepositoryService
				return GitHelper.GetGitRepositories().Select(repository => new DelegateCommand<object>(repository.Name, o => SetLocalDirectory(repository.Path)));
			}
			return Enumerable.Empty<IUICommand>();
		}


		private void SelectDirectory(object o)
		{
			var dlg = new FolderBrowserDialog { Description = "Select a Directory that contains Solutions" };
			if (Directory.Exists(TfsContext.SelectedDirectory))
				dlg.SelectedPath = TfsContext.SelectedDirectory;
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				SetLocalDirectory(dlg.SelectedPath);
			}
		}

		private void SetLocalDirectory(string path)
		{
			TfsContext.SelectedDirectory = path;
			RaisePropertiesChanged(() => SelectedWorkspaceName, () => ToolTip);
		}

		private Task LoadWorkspacesAsync(CancellationToken cancellationToken)
		{		    
		    SelectWorkspaceMenu = null;
			if (Workspaces != null && Workspaces.Any())
				Workspaces.Clear();
		    var tfsContext = TfsContext;
		    if (IsConnected && tfsContext != null)
			{
				bool invertRegex = Settings.Get(SettingsKeys.WorkspaceFilterRegexBehavoirKey, WorkspaceFilterRegexBehavoir.Disabled) == WorkspaceFilterRegexBehavoir.Inverted;
				string regex = Settings.Get(SettingsKeys.WorkspaceFilterRegexBehavoirKey, WorkspaceFilterRegexBehavoir.Disabled) == WorkspaceFilterRegexBehavoir.Disabled ? string.Empty : Settings.Get(SettingsKeys.WorkspaceFilterRegexKey, "^((?!SQL).)*$");
                
                return Task.Run(() => tfsContext.GetWorkspaces().Where(w => string.IsNullOrEmpty(regex) || (new Regex(regex).IsMatch(w.Name) != invertRegex)), cancellationToken).IgnoreCancellation(cancellationToken).ContinueWith(task => Workspaces = new ObservableCollection<Workspace>(task.Result), taskSchedulerContext).IgnoreCancellation(cancellationToken);
			}

            return Task.Delay(0, cancellationToken);
		}

		private void VersionControlServerOnWorkspacesChanged(object sender, WorkspaceEventArgs workspaceEventArgs)
		{
			Update();
		}

		protected override void OnUpdateAsynchronous(CancellationToken cancellationToken)
		{
		    if (!cancellationToken.IsCancellationRequested)
		    {
		        LoadWorkspacesAsync(cancellationToken);
		    }
		}

		private void TryLoadLastWorkspace()
		{
			if (Settings != null && IsConnected)
			{
				var name = Settings.Get(SettingsKeys.LastWorkspaceKey, string.Empty);
				if (!string.IsNullOrEmpty(name))
				{
					Workspace workspace = Workspaces.FirstOrDefault(w => w.Name == name);
					if (workspace != null && SelectedWorkspace != workspace)
						SelectedWorkspace = workspace;
				}
			}
			if (SelectedWorkspace == null || Workspaces.Select(workspace => workspace.Name).All(s => s != SelectedWorkspace.Name))
			{
				if (!TfsContext.IsLocalConnected)
				{
					var versionControlExt = serviceProvider.Get<VersionControlExt>();
					if (versionControlExt.PendingChanges.Workspace != null)
						SelectedWorkspace = versionControlExt.PendingChanges.Workspace;
					else if (versionControlExt.SolutionWorkspace != null)
						SelectedWorkspace = versionControlExt.SolutionWorkspace;
					else
						SelectedWorkspace = Workspaces.FirstOrDefault();
				}
			}
		}

	}
}