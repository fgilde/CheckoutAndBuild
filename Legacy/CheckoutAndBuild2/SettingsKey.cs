using System.Diagnostics;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2
{
	[DebuggerDisplay("{Key}")]
	public class SettingsKey
	{
		internal string Key { get; set; }

		public SerializationMode SerializationMode { get; set; }

		public bool IsGlobal => !IsWorkspaceDepending && !IsProfileDepending && !IsServerDepending && !IsTeamProjectDepending;

	    public bool IsWorkspaceDepending { get; set; }
	    public bool IsGitBranchDepending { get; set; }
		public bool IsServerDepending { get; set; }
		public bool IsTeamProjectDepending { get; set; }
		public bool IsProfileDepending { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public SettingsKey(string key, bool isGlobal = false)
			: this(key, isGlobal, SerializationMode.Xml)
		{}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public SettingsKey(string key, bool isGlobal, SerializationMode mode)
		{
			Key = key;
			SerializationMode = mode;
			//IsGlobal = isGlobal;
			IsWorkspaceDepending =
			IsGitBranchDepending = 
            IsServerDepending = 
			IsProfileDepending =
			IsTeamProjectDepending = !isGlobal;
		}
	}

	public enum SerializationMode
	{
		Xml,
		Binary
	}

	public static class SettingsKeys
	{
	    //private static string GitKey(string path, string key)
	    //{
	    //    return key + "_" + GitHelper.GetCurrentBranch(path);
	    //}

	    private static string Unique(ISolutionProjectModel projectView, string key)
	    {
	        var result = key + "_" + projectView.SolutionFileName;
	        return result;
	        //return projectView.IsGitSourceControlled ? GitKey(projectView.ItemPath, result) : result;
	    }

	    private static string Unique(WorkingFolderListViewModel model, string key)
		{            
            return key + "_" + model.Title;
        }
        private static string Unique(WorkingFolderViewModel model, string key)
		{		    
            var result = key + "_" + (model.IsLocal ? model.Directory : model.WorkingFolder.ServerItem);
		    return result;
            //return model.IsGitControlled ? GitKey(model.Directory, result) : result;
        }

		public static SettingsKey EnableSectionManagement = new SettingsKey("EnableSectionManagement", true);
		public static SettingsKey PlugInDirectoryKey = new SettingsKey("PlugInDirectory", true);
		public static SettingsKey LogLevelKey = new SettingsKey("LogLevel");
		public static SettingsKey DelphiPathKey = new SettingsKey("DelphiPath");
		public static SettingsKey RunPostScriptsAsyncKey = new SettingsKey("RunPostScriptsAsync");
		public static SettingsKey RunPreScriptsAsyncKey = new SettingsKey("RunPreScriptsAsync");        
        public static SettingsKey PreBuildScriptPathKey = new SettingsKey("PreBuildScriptPath");
		public static SettingsKey GlobalBuildPropertiesKey = new SettingsKey("GlobalBuildProperties");
		public static SettingsKey GlobalBuildTargetsKey = new SettingsKey("GlobalBuildTargets");
        public static SettingsKey PostBuildScriptPathKey = new SettingsKey("PostBuildScriptPath");
		public static SettingsKey VersionSpecKey = new SettingsKey("VersionSpec");
        public static SettingsKey HideSolutionSectionInTeamExplorerKey = new SettingsKey("HideSolutionSectionInTeamExplorer", true);
        public static SettingsKey ShowUserInfoLinkKey = new SettingsKey("ShowUserInfoLinkInTeamExplorer", true);
		public static SettingsKey LoadBuildInformationsKey = new SettingsKey("LoadExtraBuildInformations", true);
		public static SettingsKey CheckinBehaviourKey = new SettingsKey("CheckinBehaviour", true);
		public static SettingsKey HistoryTargetKey = new SettingsKey("HistoryTarget", true);
		public static SettingsKey WorkspaceFilterRegexKey = new SettingsKey("WorkspaceFilterRegex", true);
		public static SettingsKey WorkspaceFilterRegexBehavoirKey = new SettingsKey("WorkspaceFilterRegexBehavoir", true);
		public static SettingsKey AutoUpdateKey = new SettingsKey("AutoUpdate", true);
		public static SettingsKey SupportedExtensions = new SettingsKey("SupportedExtensions", true);
		public static SettingsKey SyncGitBranch = new SettingsKey("SyncGitBranch", true);
		public static SettingsKey GitSettingsPerBranch = new SettingsKey("GitSettingsPerBranch", true);
		public static SettingsKey QuickShelvesetNameKey = new SettingsKey("QuickShelvesetName", true);

		public static SettingsKey WorkItemSectionQueryKey(UserInfoContext userContext)
		{
			return new SettingsKey($"WorkItemSectionQuery{(userContext != null ? userContext.UserName : string.Empty)}");
		}

		public static SettingsKey WorkItemSectionTitleKey(UserInfoContext userContext)
		{			
			return new SettingsKey($"WorkItemSectionTitle{(userContext != null ? userContext.UserName : string.Empty)}");
		}

		public static SettingsKey SortDirectionKey(this WorkingFolderViewModel model)
		{
			string key = model.ProjectFilterFunc?.Method.ToString() ?? "";
			return new SettingsKey($"SortDirection_{(model.IsLocal ? model.Directory : model.WorkingFolder.LocalItem)}_{key}");
		}

		public static SettingsKey CustomProjectsKey(this WorkingFolderViewModel model)
		{			
			return new SettingsKey($"CustomProjects_{(model.IsLocal ? model.Directory : model.WorkingFolder.LocalItem)}_");
		}

		public static SettingsKey SortNameKey(this WorkingFolderViewModel model)
		{
			string key = model.ProjectFilterFunc?.Method.ToString() ?? "";
			return new SettingsKey($"SortName_{(model.IsLocal ? model.Directory : model.WorkingFolder.LocalItem)}_{key}");
		}

	    public static SettingsKey IsExpandedKey(this WorkingFolderListViewModel model)
	    {
            return new SettingsKey(Unique(model, "IsExpanded"));
        }

        public static SettingsKey IsExpandedKey(this WorkingFolderViewModel model)
        {
            return new SettingsKey(Unique(model, "IsExpanded"));
        }

        public static SettingsKey ServiceSettingsKey(this IOperationService operationService)
		{
			return new SettingsKey($"Service_{operationService.ServiceId}");
		}		
		public static SettingsKey ServiceSettingsKey(this IOperationService operationService, ISolutionProjectModel projectModel)
		{
			return new SettingsKey(Unique(projectModel, ServiceSettingsKey(operationService).Key));
		}

		public static SettingsKey BuildPriorityKey(this ISolutionProjectModel projectView)
		{
			return new SettingsKey(Unique(projectView, "SortIndex"));
		}

		public static SettingsKey CheckoutAndBuildActionKey(this ISolutionProjectModel projectView)
		{
			return new SettingsKey(Unique(projectView, "CheckoutAndBuildAction"));
		}

		public static SettingsKey IsIncludedKey(this ISolutionProjectModel projectView)
		{
			return new SettingsKey(Unique(projectView, "IsIncluded"));
		}

		public static SettingsKey TestSettingsFileKey(this ISolutionProjectModel projectView)
		{
			return new SettingsKey(Unique(projectView, "TestSettingsFile"));
		}

		public static SettingsKey BuildPropertiesKey(this ISolutionProjectModel projectView)
		{
			return new SettingsKey(Unique(projectView, "BuildProperties"));
		}

		public static SettingsKey BuildTargetsKey(this ISolutionProjectModel projectView)
		{
			return new SettingsKey(Unique(projectView, "BuildTargets"));
		}

		public static SettingsKey LastWorkspaceKey = new SettingsKey("LastWorkspace", true) {IsWorkspaceDepending = false, IsGitBranchDepending = false};
		public static SettingsKey LastProfileKey = new SettingsKey("LastProfileKey", true) { IsWorkspaceDepending = false, IsGitBranchDepending = false };
		public static SettingsKey AllProfilesKey = new SettingsKey("AllProfilesKey", true) { IsWorkspaceDepending = false, IsGitBranchDepending = false };
		public static SettingsKey LastDirectoryKey = new SettingsKey("LastDirectory", true);
		
	}
}