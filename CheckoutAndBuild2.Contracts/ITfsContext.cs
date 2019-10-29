using System;
using System.ComponentModel.Composition;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Project = Microsoft.TeamFoundation.WorkItemTracking.Client.Project;

namespace CheckoutAndBuild2.Contracts
{

    [InheritedExport]
    public interface ITfsContext
    {
        IServiceProvider ServiceProvider { get; }
        GitRepository CurrentGitRepository { get; }
        bool IsGitConnected { get; }
        ITeamFoundationContextManager2 TfsContextManager { get; }
        WorkItemStore WorkItemStore { get; }
        bool IsLocalConnected { get; }
        string SelectedDirectory { get; set; }
        string SelectedGitBranch { get; set; }
        Workspace SelectedWorkspace { get; set; }
        bool IsTfsConnected { get; }
        TfsTeamProjectCollection TeamProjectCollection { get; }
        VersionControlServer VersionControlServer { get; }
        IIdentityManagementService2 IdentityManagementService2 { get; }
        TfsConfigurationServer ConfigurationServer { get; }
        IBuildServer BuildServer { get; }
        Uri GetTeamUri();
        bool IsGitSourceControlled(string localItemPath);
        Project GetTeamProject();
        Project[] GetTeamProjects();
        T GetService<T>() where T : class;
    }
}