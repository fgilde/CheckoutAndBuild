using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using EnvDTE80;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.Build.Evaluation;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using GitHelper = FG.CheckoutAndBuild2.Git.GitHelper;
using Project = Microsoft.TeamFoundation.WorkItemTracking.Client.Project;

namespace FG.CheckoutAndBuild2
{
    [Export(typeof(ITfsContext))]
    public class TfsContext : NotificationObject, ITfsContext
    {
		private Workspace selectedWorkspace;
		private string selectedDirectory;
		private string selectedGitBranch;
		private WorkingProfile selectedProfile;

		public IServiceProvider ServiceProvider { get; private set; }
		public event EventHandler<ContextChangedEventArgs> ConnectionChanged;
		public event EventHandler<EventArgs<Workspace>> SelectedWorkspaceChanged;
        protected AsyncOperation AsyncOp;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public TfsContext(IServiceProvider serviceProvider)
		{
            AsyncOp = AsyncOperationManager.CreateOperation(null);
            //WorkItemsListViewHelper.Instance
            //TeamExplorerUtils.Instance			
            ServiceProvider = serviceProvider;
			selectedProfile = WorkingProfile.DefaultProfile;
			TfsContextManager = serviceProvider.Get<ITeamFoundationContextManager2>();		

			TfsContextManager.ContextChanged += ConnectionContextChanged;
			IdentityManager = new TfsIdentityManager(this);
			BuildDetailManager = new BuildDetailManager(this);
			WorkItemManager = new WorkItemManager(this);
			SelectedDirectory = GetService<SettingsService>().Get<string>(SettingsKeys.LastDirectoryKey);
		}

		public Uri GetTeamUri()
		{
			return TeamProjectCollection.Uri;
		}
	
		public bool IsGitSourceControlled(string localItemPath)
		{
            try
            {
                if (SelectedWorkspace == null || string.IsNullOrEmpty(SelectedWorkspace.TryGetServerItemForLocalItem(localItemPath)))
                {
                    return ServiceProvider.Get<DTE2>().SourceControl.IsItemUnderSCC(localItemPath) || GitHelper.IsGitControlled(localItemPath);
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
		}

		public GitRepository CurrentGitRepository
		{
			get
			{
                try
                {
                    return IsLocalConnected ? GitHelper.GetGitRepositories().FirstOrDefault(r => r.Path == SelectedDirectory) : null;
                }
                catch (Exception)
                {
                    return null;                    
                }
			}
		}

		public bool IsGitConnected => CurrentGitRepository != null || GitHelper.IsGitControlled(SelectedDirectory);

	    #region Properties

		public BuildDetailManager BuildDetailManager { get; private set; }
		public TfsIdentityManager IdentityManager { get; private set; }
		public WorkItemManager WorkItemManager { get; private set; }

		public ITeamFoundationContextManager2 TfsContextManager { get; private set; }

		public Project GetTeamProject()
		{
			if(IsTfsConnected && TfsContextManager.CurrentContext != null && TfsContextManager.CurrentContext.HasTeamProject)
				return WorkItemStore.Projects[TfsContextManager.CurrentContext.TeamProjectName];
			return null;
		}

		public Project[] GetTeamProjects()
		{
			if (IsTfsConnected && TfsContextManager.CurrentContext != null && TfsContextManager.CurrentContext.HasTeamProject)
				return WorkItemStore.Projects.OfType<Project>().ToArray();
			return null;
		}	

		public WorkItemStore WorkItemStore => TeamProjectCollection.GetService<WorkItemStore>();

        public bool IsLocalConnected => Directory.Exists(SelectedDirectory);


        public string SelectedGitBranch
        {
            get => selectedGitBranch;
            set
            {
                if (SetProperty(ref selectedGitBranch, value))
                    RaiseSelectedWorkspaceChanged();
            }
        }

        public string SelectedDirectory
		{
			get => selectedDirectory;
            set
            {                               
                GetService<SettingsService>().Set(SettingsKeys.LastDirectoryKey, value);			    
                if (SetProperty(ref selectedDirectory, value) && value != null)
                {
                    SelectedGitBranch = GitHelper.GetCurrentBranch(selectedDirectory);
					if(SelectedWorkspace != null)
						SelectedWorkspace = null;
					else
						RaiseSelectedWorkspaceChanged();
				}
			}
		}

		public WorkingProfile SelectedProfile
		{
			get => selectedProfile;
		    set
			{
				if(SetProperty(ref selectedProfile, value))
					RaiseSelectedWorkspaceChanged();
			}
		}

		public Workspace SelectedWorkspace
		{
			get
			{
				return selectedWorkspace;
			}
			set
			{
				if (selectedWorkspace != value)
				{
					selectedWorkspace = value;
					RaiseSelectedWorkspaceChanged();
					RaisePropertyChanged();
					if (value != null)
						SelectedDirectory = null;
				}				
			}
		}

		public bool IsTfsConnected
		{
			get
			{
				var service = ServiceProvider.Get<ITeamFoundationContextManager2>();
				return service?.CurrentContext != null && service.CurrentContext.HasCollection && service.CurrentContext.HasTeamProject;
			}
		}

		public TfsTeamProjectCollection TeamProjectCollection => IsTfsConnected ? ServiceProvider.Get<ITeamFoundationContextManager2>().CurrentContext.TeamProjectCollection : null;

        public VersionControlServer VersionControlServer => IsTfsConnected ? TeamProjectCollection.GetService<VersionControlServer>() : null;

        public IIdentityManagementService2 IdentityManagementService2 => IsTfsConnected ? TeamProjectCollection.GetService<IIdentityManagementService2>() : null;

        public IIdentityManagementService IdentityManagementService => IsTfsConnected ? TeamProjectCollection.GetService<IIdentityManagementService>() : null;

        public TfsConfigurationServer ConfigurationServer => VersionControlServer.TeamProjectCollection.ConfigurationServer;

        public IBuildServer BuildServer => TeamProjectCollection.GetService<IBuildServer>();

        #endregion

		#region Public Methods

		public T GetService<T>() where T:class 
		{
			return ServiceProvider.Get<T>() ?? TeamProjectCollection.GetService<T>();
		}

		public Task<Workspace[]> GetWorkspacesAsync()
		{
			return GetWorkspacesAsync(VersionControlServer.AuthorizedUser, Environment.MachineName);
		}

		public Task<Workspace[]> GetWorkspacesAsync(string userName, string machineName)
		{
			return Task.Run(() => GetWorkspaces(userName, machineName));
		}
		
		public Workspace[] GetWorkspaces()
		{
			return GetWorkspaces(VersionControlServer.AuthorizedUser, Environment.MachineName);
		}

		public Workspace[] GetWorkspaces(string userName, string machineName)
		{
			return (IsTfsConnected
				? VersionControlServer.QueryWorkspaces(null, userName, machineName)
				: new Workspace[0]).OrderBy(workspace => workspace.Name).ToArray();
		}

		#endregion
		
		#region Private Methods


		private void RaiseSelectedWorkspaceChanged()
		{			
            SelectedWorkspaceChanged?.Invoke(this, new EventArgs<Workspace>(SelectedWorkspace));
		}

		private void ConnectionContextChanged(object sender, ContextChangedEventArgs e)
		{            
            SelectedWorkspace = null;
            //ConnectionChanged?.Invoke(sender, e);
            AsyncOp.Post(state => ConnectionChanged?.Invoke(sender,e), e);
        }

	

		#endregion

	}
}