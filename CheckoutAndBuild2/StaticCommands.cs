using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.Build;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;
using SolutionPacker;

namespace FG.CheckoutAndBuild2
{

    public static class StaticCommands
	{
        public static IUICommand ManageWorkspacesCommand = new DelegateCommand<object>("Manage Workspaces...", ManageWorkspaces) {IconImage = Images.ManageCounterSets_8769.ToImageSource()};
		public static IUICommand ConnectToSourceControlCommand = new DelegateCommand<object>("Connect to SourceControl...", ConnectToSourceControlProvider) { IconImage = Images.SourceControl_16xLG.ToImageSource() };


		public static IUICommand AboutCommand = new DelegateCommand<object>("?", ShowAbout);
		public static IUICommand SettingsCommand = new DelegateCommand<object>("Main Settings", OpenMainSettings);
		public static IUICommand SectionSettingsCommand = new DelegateCommand<object>("Configure Sections", OpenSectionSettings);
		public static IUICommand ResetSettingsCommand = new DelegateCommand<object>("Reset all Settings", ResetSettings);


		public static IUICommand CheckinCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Checkin Pending Changes...", TryCheckin, CanCheckin) { IconImage = Images.CheckIn_13188.ToImageSource(), Tag = new MenuItemSettings() {IsCheckInCommand = true, FontWeight = FontWeights.Bold, IsVisibleInQuicklist = true} };

		public static DelegateCommand<IEnumerable<ProjectViewModel>> OpenProjectsCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Open Selected Projects", OpenProjects, models => models != null && models.Any()) {IconImage = Images.OpenFileDialog_692.ToImageSource(), Tag = new MenuItemSettings { IsVisibleInQuicklist = true} };	  
	    public static DelegateCommand<IEnumerable<ProjectViewModel>> OpenFolderCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Open in File Explorer", OpenInFileExplorer, models => models != null && models.Any()) { IconImage = Images.folder_Open_16xLG.ToImageSource() };

		public static DelegateCommand<IEnumerable<ProjectViewModel>> OpenInSourceControlExplorerCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Open in Source Control Explorer", OpenInSourceControl, models => models != null && models.Count() == 1) { IconImage = Images.SourceControl_16xLG.ToImageSource() };
		public static DelegateCommand<IEnumerable<ProjectViewModel>> MergeToOneSolutionCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Merge to One Solution", MergeSolutions, models => models != null && models.Count() > 1 && models.All(model => !model.IsDelphiProject)) { IconImage = Images.Solution_8308.ToImageSource() };
       
        public static DelegateCommand<IEnumerable<ProjectViewModel>> ShowHistoryCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Show History...", ShowHistory, CanShowHistory) { IconImage = Images.History_16xLG.ToImageSource() };
		
		public static DelegateCommand<IEnumerable<ProjectViewModel>> Seperator = new DelegateCommand<IEnumerable<ProjectViewModel>>(models => {}) {IsSeparator = true};
		public static DelegateCommand<IEnumerable<ProjectViewModel>> StartExecutableCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Start", Run, models => models != null && models.Any()) { IconImage = Images.startwithoutdebugging_6556.ToImageSource() };
        public static DelegateCommand<IEnumerable<ProjectViewModel>> StartExecutableWithDebuggerCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Start and Attach Debugger", RunWithDebugger, models => models != null && models.Count() == 1) { IconImage = Images.startwithdebugging_6556.ToImageSource() };

        public static DelegateCommand<IEnumerable<ProjectViewModel>> RestartExecutableCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Restart", Restart) { IconImage = Images.refresh_16xLG.ToImageSource(), Tag = new MenuItemSettings { IsVisibleAsync = CanStopAnyAsync, IsVisibleInQuicklist = true} };
        public static DelegateCommand<IEnumerable<ProjectViewModel>> StopExecutableCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Stop", models => Stop(models)) { IconImage = Images.Terminateprocess_6569.ToImageSource(), Tag = new MenuItemSettings { IsVisibleAsync = CanStopAnyAsync, IsVisibleInQuicklist = true } };


        public static DelegateCommand<IEnumerable<ProjectViewModel>> OpenOutputFolderCommand = new DelegateCommand<IEnumerable<ProjectViewModel>>("Open Output Directory", OpenOutputInFileExplorer, models => models != null && models.Any()) { IconImage = Images.folder_Open_16xLG.ToImageSource() };
        public static DelegateCommand<object> ShowPropertiesCommand = new DelegateCommand<object>("Show Properties", ShowPropertyInspector, models => true) { IconImage = Images.ManageCounterSets_8769.ToImageSource(), InputGestureText = "F4", Tag = new MenuItemSettings { IsVisibleInQuicklist = true } };


        private static async void MergeSolutions(IEnumerable<ProjectViewModel> obj)
        {
            var packers = await MergeSolutionsAsync(obj);
            ShowResultStatus(packers);
        }

        internal static async Task<Packer[]> MergeSolutionsAsync(IEnumerable<ProjectViewModel> obj, string newSolutionName = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var solutionName = newSolutionName ?? $"!Merged_Build_{DateTime.Now.ToString().Replace(new[] { ".", ":", ",", "!", "<", ">" }, "_")}.sln";
            return await Task.WhenAll(obj.GroupBy(model => model.ParentWorkingFolder.Directory ?? model.ParentWorkingFolder.WorkingFolder.LocalItem).Select(models =>new Packer(Path.Combine(models.Key, solutionName),models.Select(model => model.ItemPath).ToArray()).ExecuteAsync(cancellationToken)));
        }

        private static void ShowResultStatus(Packer[] packers)
        {
            foreach (var packer in packers)
            {
                Guid id = Guid.NewGuid();
                var openSolutionCommand = new DelegateCommand<object>(param =>
                {
                    Output.HideNotification(id);
                    ProjectViewModel.OpenInVisualStudio(packer.OutputSolutionFileName);
                }, param => true);

                Output.Notification($"Successfully created [{packer.OutputSolutionFileName}](Click to open solution).",
                    NotificationType.Information, NotificationFlags.None, openSolutionCommand, id);
            }
        }

        private static async Task<bool> CanStopAnyAsync(IEnumerable<ProjectViewModel> projectViewModels)
        {
            if (projectViewModels == null || !projectViewModels.Any())                            
                return false;
            IEnumerable<Task<bool>> tasks = projectViewModels.Select(model => model.HasRunningInstancesAsync(false));
            foreach (var t in tasks)
            {
                if (await t)
                    return true;
            }
            return false;      
        }

        private static async Task Stop(IEnumerable<ProjectViewModel> solutions)
	    {
            var token = GetCancellationToken();
            using (new PauseCheckedActionScope(() => solutions.SetOperations(Operations.Stopping), () => solutions.SetOperations(Operations.None)))
            {
                await Task.WhenAll(solutions.Select(model => model.KillRunningInstancesAsync(false, token)));
            }
        }

        private static async void Restart(IEnumerable<ProjectViewModel> solutions)
        {
            if (solutions != null && solutions.Any())
            {
                var arr = solutions.ToArray();
                await Stop(arr);
                Run(arr);
            }
        }

        private static async void Run(IEnumerable<ProjectViewModel> solutions)
        {
            var token = GetCancellationToken();
            using (new PauseCheckedActionScope(() => solutions.SetOperations(Operations.Starting), () => solutions.SetOperations(Operations.None)))
            {
                await Task.WhenAll(solutions.Select(model => model.StartAsync(false, token)));
            }
        }

        private static async void RunWithDebugger(IEnumerable<ProjectViewModel> solutions)
        {
            var token = GetCancellationToken();
            using (new PauseCheckedActionScope(() => solutions.SetOperations(Operations.Starting), () => solutions.SetOperations(Operations.None)))
            {
                await Task.WhenAll(solutions.Select(model => model.StartAsync(true, token)));
            }
        }


        private static void ShowPropertyInspector(object obj)
		{
			if (obj is IEnumerable<ProjectViewModel>)
				VisualStudioTrackingSelection.UpdateSelectionTracking(((IEnumerable<ProjectViewModel>)obj).Select(model => model.OptionsObject).ToArray());
			else if(obj is ProjectViewModel)
				VisualStudioTrackingSelection.UpdateSelectionTracking(((ProjectViewModel)obj).OptionsObject);
			else if (obj != null)
				VisualStudioTrackingSelection.UpdateSelectionTracking(obj);
			var shell = CheckoutAndBuild2Package.GetGlobalService<IVsUIShell>();
			Guid guidPropBrowser = new Guid(ToolWindowGuids.PropertyBrowser);
			IVsWindowFrame frameProperties;
			shell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref guidPropBrowser, out frameProperties);
			frameProperties.Show();
		}


		public static DelegateCommand<string> UserInfoCommand = new DelegateCommand<string>(DisplayUserInfo);
		public static DelegateCommand<int> ViewChangesetDetailsCommand = new DelegateCommand<int>(ViewChangesetDetails);
		public static DelegateCommand<IBuildDetail> ViewBuildDetailsCommand = new DelegateCommand<IBuildDetail>(ViewBuildDetails);
		public static DelegateCommand<IBuildDefinition> ViewBuildDefinitionCommand = new DelegateCommand<IBuildDefinition>(ViewBuildDefinition);
		public static DelegateCommand<WorkItem> NavigateToWorkItemCommand = new DelegateCommand<WorkItem>(NavigateToWorkItem);

		private static T Get<T>()
			where T : class
		{
			return CheckoutAndBuild2Package.GetGlobalService<T>();
		}

		private static void NavigateToWorkItem(WorkItem workItem)
		{
			Get<TfsContext>().WorkItemManager.NavigateToWorkItem(workItem);
		}

		private static void ViewBuildDefinition(IBuildDefinition definition)
		{
			VsTeamFoundationBuild vsTfBuild = (VsTeamFoundationBuild)Get<IVsTeamFoundationBuild>();
			if (vsTfBuild != null)
				vsTfBuild.DefinitionManager.OpenDefinition(definition.Uri);
		}

		private static void ViewBuildDetails(IBuildDetail detail)
		{
			VsTeamFoundationBuild vsTfBuild = (VsTeamFoundationBuild)Get<IVsTeamFoundationBuild>();
		    vsTfBuild?.DetailsManager.OpenBuild(detail.Uri);
		}

		private static void OpenOutputInFileExplorer(IEnumerable<ProjectViewModel> solutions)
		{
			if (solutions != null)
			{
				var projectViewModels = solutions as ProjectViewModel[] ?? solutions.ToArray();
				var directories = projectViewModels.GetOutputDirectories(false, false).ToList();
				if (directories.Any())
				{
					if (directories.Count == 1)
						Process.Start(directories[0]);
					else
					{
						directories.Select(d =>
						{
							string label = d;
							projectViewModels.Apply(model => label = label.Replace(model.SolutionFolder, string.Empty));
							return new DelegateCommand<object>(label, o => Process.Start(d));
						}).Cast<IUICommand>().ToList().ToContextMenu().IsOpen = true;						
					}
				}
			}
		}

		private static void ViewChangesetDetails(int changesetId)
		{
			Get<ITeamExplorer>().NavigateToPage(new Guid(TeamExplorerPageIds.ChangesetDetails), changesetId);
		}

		private static void DisplayUserInfo(string userName)
		{
			TfsContext tfsContext = Get<TfsContext>();
			var teamFoundationIdentity = tfsContext.IdentityManager.GetIdentity(userName);
			Get<ITeamExplorer>().NavigateToPage(GuidList.userInfoPage.ToGuid(), new UserInfoContext(teamFoundationIdentity));
		}

		public static IEnumerable<IUICommand> All
		{
			get
			{
				return typeof(StaticCommands).GetFields().Where(info => typeof(IUICommand).IsAssignableFrom(info.FieldType))
					.Select(info => info.GetValue(null) as IUICommand);
			}
		}

		public static void RaiseAllCanExecuteChanged()
		{
			foreach (IUICommand uiCommand in All)
				uiCommand.RaiseCanExecuteChanged();
		}

		public static IEnumerable<IUICommand> Merge(IUICommand command1, IUICommand command2, params IUICommand[] otherCommands)
		{
			yield return command1;
			yield return command2;
			foreach (var otherCommand in otherCommands.Where(command => command != null))
				yield return otherCommand;
		}

		private static CancellationToken GetCancellationToken()
		{
			return Get<MainViewModel>().CreateCancellationToken();
		}

		private static void ShowAbout(object obj)
		{
			Get<ITeamExplorer>().NavigateToPage(GuidList.aboutPageId.ToGuid(), null);
		}

		private static bool CanShowHistory(IEnumerable<ProjectViewModel> models)
		{
			if (models == null)
				return false;
			var projectViewModels = models as ProjectViewModel[] ?? models.ToArray();
			if(!projectViewModels.Any())
				return false;

			var settingsService = Get<SettingsService>();
			if (settingsService.Get(SettingsKeys.HistoryTargetKey, HistoryTarget.VisualStudioPage) == HistoryTarget.TeamExplorer)
				return projectViewModels.Length == 1;
			return true;
		}

		private static void ShowHistory(IEnumerable<ProjectViewModel> obj)
		{
			var settingsService = Get<SettingsService>();
			if (settingsService.Get(SettingsKeys.HistoryTargetKey, HistoryTarget.VisualStudioPage) == HistoryTarget.TeamExplorer)
				Get<ITeamExplorer>().NavigateToPage(GuidList.historyPage.ToGuid(), obj.First().ServerItemPath);
			else
				obj.Apply(model => Get<VersionControlExt>().History.Show(model.SolutionFolder, VersionSpec.Latest, 0, RecursionType.Full));
		}

		private static bool CanCheckin(IEnumerable<ProjectViewModel> arg)
		{
			if (arg != null)
			{
				return arg.Any(model => model.HasChanges);
			}
			return false;
		}

		private static void TryCheckin(IEnumerable<ProjectViewModel> obj)
		{
			// TODO: Git checkin
			//TeamExplorerPageIds.GitChanges
			if (obj == null)
				return;
			IServiceProvider provider = Get<CheckoutAndBuild2Package>();
			var tfsContext = provider.Get<TfsContext>();
			var settingsService = provider.Get<SettingsService>();
			var models = obj.ToArray();
			if (models.Any() && tfsContext.SelectedWorkspace != null)
			{
				ItemSpec[] specs = models.Select(projectViewModel => new ItemSpec(projectViewModel.SolutionFolder, RecursionType.Full)).ToArray();
				var changes = tfsContext.SelectedWorkspace.GetPendingChanges(specs);
				if (changes != null && changes.Any())
				{
					if (settingsService.Get(SettingsKeys.CheckinBehaviourKey, CheckinBehaviour.CheckinDialog) ==
						CheckinBehaviour.CheckinDialog)
					{
						TeamControlFactory.ShowCheckinDialog(tfsContext.SelectedWorkspace, changes);
						models.Apply(model => model.BeginLoadPendingChanges());
					}
					else
					{
						TeamExplorerUtils.Instance.NavigateToPage(TeamExplorerPageIds.PendingChanges, provider, changes);
					}
				}
				else
					Output.Notification("There are no changes to checkin!");
			}
		}

		private static void OpenSectionSettings(object param)
		{
			CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>().ShowOptionPage(typeof(CheckoutAndBuildSectionOptionsPage));
		}

		private static void OpenMainSettings(object param)
		{		
			Get<SettingsService>().ShowMainSettingsPage();
		}

		private static void ResetSettings(object o)
		{
			var resetSettingsCommand = new DelegateCommand<object>(param =>
			{
				Output.HideNotification(GuidList.resetNotificationId.ToGuid());
				Get<SettingsService>().Reset();
			}, param => true);

			Output.Notification("Are you sure to Reset all C&B Settings?  [Reset all Settings](Click to reset all settings).",
				NotificationType.Warning, NotificationFlags.None, resetSettingsCommand, GuidList.resetNotificationId.ToGuid());
		}

		private static void OpenInFileExplorer(IEnumerable<ProjectViewModel> obj)
		{
			foreach (ProjectViewModel projectViewModel in obj.Where(model => Directory.Exists(model.SolutionFolder)))
				Process.Start(projectViewModel.SolutionFolder);
		}

		private static void OpenInSourceControl(IEnumerable<ProjectViewModel> obj)
		{
			Get<VersionControlExt>().Explorer.Navigate(obj.First().ServerItem);
		}

	    private static IEnumerable<IUICommand> VSChilds()
	    {
	        return VisualStudioPluginHelper.GetInstalledStudios().Select(info =>
	        {
	            return new DelegateCommand<IEnumerable<ProjectViewModel>>(info.Name, models => OpenProjects(models, info), models => true);
	        });
	    }

	    private static void OpenProjects(IEnumerable<ProjectViewModel> projectViewModels)
	    {
	        OpenProjects(projectViewModels, null);
	    }

        private static void OpenProjects(IEnumerable<ProjectViewModel> projectViewModels, VisualStudioInfo visualStudio)
		{
		    Action openAction = () =>
		    {
		        Task.Run(async () =>
		        {
		            bool forceNewInstance = false;
		            foreach (var projectViewModel in projectViewModels)
		            {
		                await projectViewModel.OpenProjectAsync(forceNewInstance, visualStudio);
		                forceNewInstance = true;
		            }
		        });
		    };
		    var viewModels = projectViewModels as ProjectViewModel[] ?? projectViewModels.ToArray();
		    if (viewModels.Length >= 5)
		    {
                Output.Notification($"You try to open {viewModels.Length} Solutions. This operation may take some time in demanding, are you sure you want to proceed [Continue opening solutions](Click to continue).",
               NotificationType.Warning, NotificationFlags.None, openAction);

            }
		    else
		        openAction();
        }

		private static void ConnectToSourceControlProvider(object obj)
		{
			Get<ITeamExplorer>().NavigateToPage(new Guid(TeamExplorerPageIds.Connect), null);
		}

		private static void ManageWorkspaces(object o)
		{
			TeamControlFactory.ShowDialogManageWorkspaces();
		}
	}
}