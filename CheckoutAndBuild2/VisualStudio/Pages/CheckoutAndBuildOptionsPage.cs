using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [CLSCompliant(false), ComVisible(true)]
	[Guid(GuidList.mainOptionsPage)]
	public class CheckoutAndBuildOptionsPage : DialogPage
    {
        private readonly SettingsService settingsService;
        
        /// <summary>
		/// Initializes a new instance of <see cref="T:Microsoft.VisualStudio.Shell.DialogPage"/>.
		/// </summary>
		public CheckoutAndBuildOptionsPage()
		{
		    settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
		}

        [Browsable(true), DisplayName("On Git use Settings per branch")]
        [Category("Git")]
        [Description("Set this to true, to use custom settings for every branch")]
        public bool GitSettingsPerBranch
        {
            get => settingsService.Get(SettingsKeys.GitSettingsPerBranch, true);
            set => settingsService.Set(SettingsKeys.GitSettingsPerBranch, value);
        }

        [Browsable(true), DisplayName("Sync selected GitBranch (Experimentell)")]
        [Category("Git")]
        [Description("Set this to true, to sync active branch with current COAB Settings for a branch")]
        public bool SyncGitBranch
        {
            get => settingsService.Get(SettingsKeys.SyncGitBranch, true);
            set => settingsService.Set(SettingsKeys.SyncGitBranch, value);
        }

        [Browsable(true), DisplayName("Supported Extensions")]
        [Category("Miscellaneous")]
        [Description("Setup Project extensions you want to manage with CheckoutAndBuild. (This changes affect after VS Restart)")]
        public string[] SupportedExtensions
        {
            get => settingsService.Get(SettingsKeys.SupportedExtensions, Const.DefaultSupportedProjectExtensions);
            set => settingsService.Set(SettingsKeys.SupportedExtensions, value);
        }

        [Browsable(true), DisplayName("Automtic Updates")]
		[Category("Updates")]
		[Description("This settings allows you to setup how " + Const.ApplicationName + " should check automatically for updates")]
		public AutoUpdateBehavior AutoUpdate
		{
			get => settingsService.Get(SettingsKeys.AutoUpdateKey, AutoUpdateBehavior.DownloadAndInstall);
		    set => settingsService.Set(SettingsKeys.AutoUpdateKey, value);
		}

        [Browsable(true), DisplayName("Hide Solution-Section in Team Explorer")]
		[Category("Miscellaneous")]
		[Description("Set this to true to overwrite the Solution section in TeamExplorer Homepage with the CheckoutAndBuild section")]
        public bool HideSolutionSectionInTeamExplorer
		{
			get => settingsService.Get(SettingsKeys.HideSolutionSectionInTeamExplorerKey, true);
            set
            {
                settingsService.Set(SettingsKeys.HideSolutionSectionInTeamExplorerKey, value);
                ITeamExplorer teamExplorer = CheckoutAndBuild2Package.GetGlobalService<ITeamExplorer>();
                var solutionsSection = teamExplorer?.CurrentPage?.GetSections().FirstOrDefault(section => section.GetType().FullName == "Microsoft.VisualStudio.TeamFoundation.TeamExplorer.Home.SolutionsSection");
                if (solutionsSection != null)
                    SectionManager.SetSectionIsIncluded(solutionsSection, !value);
            }
		}

        [Browsable(true), DisplayName("Show UserInfoLink")]
        [Category("Miscellaneous")]
        [Description("Set this to true to Show a Link to UserInfoPage on the TeamExplorer")]
        public bool ShowUserInfoLink
        {
            get => settingsService.Get(SettingsKeys.ShowUserInfoLinkKey, true);
            set
            {
                settingsService.Set(SettingsKeys.ShowUserInfoLinkKey, value);
                ITeamExplorer teamExplorer = CheckoutAndBuild2Package.GetGlobalService<ITeamExplorer>();
                teamExplorer?.CurrentPage?.Refresh();
            }
        }

        [Browsable(true), DisplayName("Load Build Informations for Changeset Infos")]
        [Category("Miscellaneous")]
        [Description("If this is enabled you can see the triggered Build for every changeset in changeset detail page and in recent changes page (Can be a performance issue for VS)")]
        public bool LoadBuildsForChangesets
        {
            get => settingsService.Get(SettingsKeys.LoadBuildInformationsKey, false);
            set => settingsService.Set(SettingsKeys.LoadBuildInformationsKey, value);
        }

        [Browsable(true), DisplayName("Behaviour for Checkin Command")]
		[Category("Behaviours")]
		[Description("Use this to define the way for a checkin triggerred from CheckoutAndBuild. CheckinDialog will open a Dialog to checkin and PendingChangePage will navigate to the PendingCHange Page in VS TeamExplorer")]
		public CheckinBehaviour CheckinBehaviour
		{
			get => settingsService.Get(SettingsKeys.CheckinBehaviourKey, CheckinBehaviour.CheckinDialog);
            set => settingsService.Set(SettingsKeys.CheckinBehaviourKey, value);
        }

		[Browsable(true), DisplayName("Target for Show History Command")]
		[Category("Behaviours")]
		[Description("Use TeamExplorer to display history from ContextMenu in the TeamExplorer otherwise the VisualStudio Window will apear")]
		public HistoryTarget HistoryTarget
		{
			get => settingsService.Get(SettingsKeys.HistoryTargetKey, HistoryTarget.VisualStudioPage);
		    set => settingsService.Set(SettingsKeys.HistoryTargetKey, value);
		}

		[Browsable(true), DisplayName("WorkspaceFilter Expression")]
		[Category("Workspace List Filter")]
		[Description("Create a regular expression to filter only specific workspaces in workspace selector drop down (e.g '^((?!FB).)*$' (only workspaces with that containf FB in Name) )")]
		public string WorkspaceFilterRegex
		{
			get => settingsService.Get(SettingsKeys.WorkspaceFilterRegexKey, "^((?!SQL).)*$");
		    set => settingsService.Set(SettingsKeys.WorkspaceFilterRegexKey, value);
		}

		[Browsable(true), DisplayName("WorkspaceFilter Behavoir")]
		[Category("Workspace List Filter")]
		[Description("Here you can setup how your WorkspaceFilter Expression should work")]
		public WorkspaceFilterRegexBehavoir WorkspaceFilterRegexBehavoir
		{
			get => settingsService.Get(SettingsKeys.WorkspaceFilterRegexBehavoirKey, WorkspaceFilterRegexBehavoir.Disabled);
		    set => settingsService.Set(SettingsKeys.WorkspaceFilterRegexBehavoirKey, value);
		}



		//[Browsable(false)]
		//[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		//protected override IWin32Window Window => (pageControl = new OptionsPageControl {OptionsPage = this});

  //      /// <summary>
	 //   /// Handles Windows Activate messages from the Visual Studio environment.
	 //   /// </summary>
	 //   /// <param name="e">[in] Arguments to event handler.</param>
	 //   protected override void OnActivate(CancelEventArgs e)
	 //   {
		//    base.OnActivate(e);
	 //   }

	    /// <summary>
	    /// Handles Apply messages from the Visual Studio environment.
	    /// </summary>
	    /// <param name="e">[in] Arguments to event handler.</param>
	    protected override void OnApply(PageApplyEventArgs e)
	    {
		    base.OnApply(e);
            CheckoutAndBuild2Package.GetGlobalService<TfsContext>().BuildDetailManager.SetEnabled(LoadBuildsForChangesets);
	    }
	}

    
	public enum WorkspaceFilterRegexBehavoir
	{
	    [Description("Normal execution")]	    
        Normal,
	    [Description("Inverted execution")]
        Inverted,
	    [Description("Regex disabled")]
        Disabled
	}

	public enum AutoUpdateBehavior
	{
		DownloadAndInstall,
		NotificationOnly,
		None
	}

	public enum HistoryTarget
	{
		TeamExplorer,
		VisualStudioPage
	}

	public enum CheckinBehaviour
	{
		CheckinDialog,
		PendingChangePage
	}

}