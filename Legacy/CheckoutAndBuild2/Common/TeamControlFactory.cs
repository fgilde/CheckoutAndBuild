using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Controls;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.Common
{
	public static class TeamControlFactory
	{
		public static QueryItem LastQueryItem { get; private set; }
		
		public static TfsContext TfsContext
		{
			get { return Package.GetGlobalService(typeof(TfsContext)) as TfsContext; }
		}

		public static Form ShowDialog(dynamic formOrControl, bool modal)
		{
			if (formOrControl is Form)
				return ShowDynamicDialog(formOrControl, modal);
			var frm = new Form();
			frm.Controls.Add((Control)formOrControl);
			((Control)formOrControl).Dock = DockStyle.Fill;
			frm.MinimumSize = new Size(500, 400);
			frm.Text = ((Control)formOrControl).Text;
			if (modal)
				frm.ShowDialog();
			else
				frm.Show();

			return frm;
		}

		public static bool ShowCheckinDialog(Workspace workspace, PendingChange[] changes, int selectedTabIndex = 0)
		{
			if (workspace == null)
				workspace = TfsContext.SelectedWorkspace;
			var checkIndialog = new CheckInDialog(TfsContext.VersionControlServer, changes);
			return checkIndialog.ShowDialog(workspace, selectedTabIndex);
		}

		public static bool ShowCheckinDialog(Workspace workspace = null, WorkingFolder workFolder = null, int selectedTabIndex = 0)
		{
			if (workspace == null)
				workspace = TfsContext.SelectedWorkspace;
			var checkIndialog = new CheckInDialog(TfsContext.VersionControlServer, workFolder);
			return checkIndialog.ShowDialog(workspace, selectedTabIndex);
		}

		private static Form ShowDynamicDialog(dynamic df, bool modal)
		{
			var d = (Form)df;
			d.StartPosition = FormStartPosition.CenterParent;
			//d.SetPositionCenteredToOwner(Application.OpenForms[0]);
			if (modal)
				d.ShowDialog();
			else
				d.Show();
			return d;
		}

		public enum PropertiesMode
		{
			Explorer,
			Checkin,
			Shelve,
			Unshelve,
			Changeset,
			Online,
		}

		public static void ResolveConflictsVS(Workspace workspace, string[] pathFilter = null, bool recursive = true, bool afterCheckin = false)
		{
			Assembly assembly = typeof(Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlLabelExt).Assembly;
			var managerType = assembly.GetType("Microsoft.VisualStudio.TeamFoundation.VersionControl.ResolveConflictsManager");
			object manager = Activator.CreateInstance(managerType);
			var methodInfo = managerType.GetMethod("ResolveConflicts", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			var initMethod = managerType.GetMethod("Initialize");
			initMethod.Invoke(manager, new object[] { });
			methodInfo.Invoke(manager, new object[] { workspace, pathFilter, recursive, afterCheckin });
		}

		public static void ShowDialogFileFolderProperties(Workspace workspace, PendingChange pendingChange, PropertiesMode mode)
		{			
			Assembly assembly = typeof(ConfigureWorkspaceGrid).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogFileFolderProperties");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer, workspace, pendingChange, mode);

			var d = (Form)dynamicDialog;
			d.ShowDialog();
		}


		public static void ShowDialogChangesetDetails(Workspace workspace, int changesetId)
		{
			//internal DialogChangesetDetails(VersionControlServer sourceControl, Workspace workspace, int changesetId, bool readOnly, bool allowSaveChanges)
			Assembly assembly = typeof(ConfigureWorkspaceGrid).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogChangesetDetails");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer, workspace, changesetId, false, true);

			var d = (Form)dynamicDialog;
			d.ShowDialog();
		}

		public static dynamic CreateDialogManageWorkspaces()
		{
			Assembly assembly = typeof(ConfigureWorkspaceGrid).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogManageWorkspaces");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer);
			return dynamicDialog;
		}

		public static dynamic CreateDialogCompareWindow()
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogCompare");
			dynamic dynamicDialog = ExposedObject.New(dialogType, string.Empty, string.Empty, TfsContext.VersionControlServer);
			return dynamicDialog;
		}

		public static Form ShowDialogCompareWindow(bool modal = true)
		{
			return ShowDynamicDialog(CreateDialogCompareWindow(), modal);
		}

		public static dynamic CreateSourceControlExplorer()
		{
			Assembly assembly = typeof(WorkItemPolicy).Assembly;
			var controlType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.ExplorerScc");
			var result = ExposedObject.New(controlType);
			return result;
		}

		public static dynamic CreateUserIdentityInfoDialog(TeamFoundationIdentity identity = null)
		{
			if (identity == null)
				identity = TfsContext.ConfigurationServer.AuthorizedIdentity;
			Assembly assembly = typeof(Microsoft.TeamFoundation.Controls.WinForms.PermissionEvents).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.Controls.WinForms.UserPropertiesDialog");
			dynamic dynamicDialog = ExposedObject.New(dialogType, identity, TfsContext.ConfigurationServer, "0");
			return dynamicDialog;
		}

		public static Form ShowUserIdentityInfoDialog(TeamFoundationIdentity identity = null, bool modal = true)
		{
			return ShowDynamicDialog(CreateUserIdentityInfoDialog(identity), modal);
		}

		public static Form ShowDialogManageWorkspaces()
		{
			dynamic dynamicDialog = CreateDialogManageWorkspaces();
			var d = (Form)dynamicDialog;
			var lv = (ListView)dynamicDialog.listViewWorkspaces;
			var localWorkspaces = (Dictionary<Workspace, bool>)dynamicDialog.m_localWorkspaces;

			d.Load += (o, args) =>
			{
				var workspaces = TfsContext.GetWorkspaces();
				if (lv.Items.Count != workspaces.Count())
				{
					foreach (Workspace w in workspaces)
					{
						var listViewItem = new ListViewItem();
						dynamicDialog.workspaceToItem(w, listViewItem);
						lv.Items.Add(listViewItem);
						localWorkspaces[w] = true;
					}
				}
			};
			return ShowDynamicDialog(d, true);
		}

		//public static string GetTeamProject()
		//{
		//	List<CatalogNode> teamProjects = tfs.TeamProjectInfos;
		//	if (teamProjects.Any())
		//	{
		//		if (teamProjects.Count == 1)
		//		{
		//			var project = teamProjects[0];
		//			return project.Resource.DisplayName;
		//		}
		//		var picker = new TeamProjectPicker(TeamProjectPickerMode.SingleProject, true, new UICredentialsProvider());
		//		picker.AcceptButtonText = "Select";
		//		picker.Text = "Select Team Project";
		//		picker.SelectedTeamProjectCollection = tfs.TeamProjectCollection;
		//		if (picker.ShowDialog() == DialogResult.OK)
		//		{
		//			ProjectInfo project = picker.SelectedProjects[0];
		//			return project.Name;
		//		}
		//	}
		//	return null;
		//}

		public static dynamic CreateDialogQueueBuild(string teamProject, int defaultBuildDefinitionIndex = 0)
		{
			if (string.IsNullOrEmpty(teamProject))
				teamProject = TfsContext.TfsContextManager.CurrentContext.TeamProjectName;
			if (!string.IsNullOrEmpty(teamProject))
			{
				Assembly assembly = typeof(Microsoft.TeamFoundation.Build.Controls.ControlBuildDefinitionDefaultsTab).Assembly;
				var dialogType = assembly.GetType("Microsoft.TeamFoundation.Build.Controls.DialogQueueBuild");

				IBuildDefinition[] buildDefinitions = TfsContext.BuildServer.QueryBuildDefinitions(teamProject);
				IBuildController[] queryBuildControllers = TfsContext.BuildServer.QueryBuildControllers();
				dynamic dynamicDialog = ExposedObject.New(dialogType, teamProject, buildDefinitions, defaultBuildDefinitionIndex, queryBuildControllers,
														  TfsContext.TeamProjectCollection);
				return dynamicDialog;
			}
			return null;
		}

		public static Form ShowDialogQueueBuild(string teamProject = "", bool modal = true, int defaultBuildDefinitionIndex = 0)
		{
			var d = CreateDialogQueueBuild(teamProject, defaultBuildDefinitionIndex);
			return d != null ? ShowDynamicDialog(d, modal) : null;
		}

		public static string BrowseServerFolder(Workspace workspace, string serverPath)
		{
			Assembly assembly = typeof(ConfigureWorkspaceGrid).Assembly;
			var modelType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.Model");
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogHatFolderBrowser");

			ConstructorInfo[] constructorInfosDialog = dialogType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance);
			ConstructorInfo[] constructorInfosModel = modelType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance);

			object[] modelParams = new object[]
				                   	{
										TfsContext.VersionControlServer,
										workspace,
										null,
				                   	};
			object model = constructorInfosModel[0].Invoke(modelParams);

			object[] dialogParams = new[]
				                   	{
										model,
										serverPath,
										false
				                   	};

			var d = (Form)constructorInfosDialog[0].Invoke(dialogParams);
			if (d.ShowDialog() == DialogResult.OK)
			{
				//FolderPath
				var path = d.GetType().GetProperty("FolderPath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(d, null) as string;
				return path;
			}
			return string.Empty;
		}

		public static dynamic CreateDialogMove(string fromPath, string initialToPath, bool fromPathIsFolder)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogMove");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer, TfsContext.SelectedWorkspace, fromPath, initialToPath, fromPathIsFolder);
			return dynamicDialog;
		}

		public static Form ShowDialogMove(string fromPath, string initialToPath, bool fromPathIsFolder, bool modal = true)
		{
			return ShowDynamicDialog(CreateDialogMove(fromPath, initialToPath, fromPathIsFolder), modal);
		}

		public static Form ShowDialogCheckout(bool modal, bool exclusiveCheckout, bool getLatestOnCheckout, params string[] items)
		{
			return ShowDynamicDialog(CreateDialogCheckout(exclusiveCheckout, getLatestOnCheckout, items), modal);
		}

		public static dynamic CreateDialogCheckout(bool exclusiveCheckout, bool getLatestOnCheckout, params string[] items)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogCheckout");
			dynamic dynamicDialog = ExposedObject.New(dialogType, items, exclusiveCheckout, getLatestOnCheckout);
			return dynamicDialog;
		}

		public static Form ShowDialogBranch(string fromItem, bool modal = true)
		{
			return ShowDynamicDialog(CreateDialogBranch(fromItem), modal);
		}

		public static dynamic CreateDialogBranch(string fromItem)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogBranch");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.SelectedWorkspace);
			dynamicDialog.FromItem = fromItem;
			return dynamicDialog;
		}

		public static Item ShowDialogChooseItem(string initialFolder = "$/")
		{
			var dlg = CreateDialogChooseItem(initialFolder);
			var form = ShowDynamicDialog(dlg, true);
			if (((Form)form).DialogResult == DialogResult.OK)
			{
				var res = dlg.SelectedItem;
				return (Item)res;
			}
			return null;
		}

		public static dynamic CreateDialogChooseItem(string initialFolder = "$/")
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogChooseItem");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer, initialFolder, string.Empty);
			return dynamicDialog;
		}

		public static dynamic CreateDialogChooseVersion(VersionSpec version, string initialFolder = "$/")
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogChooseVersion");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer, initialFolder, version, 0);
			return dynamicDialog;
		}

		public static VersionSpec ShowDialogChooseVersion(VersionSpec version, string initialFolder = "$/")
		{
			dynamic dlg = CreateDialogChooseVersion(version, initialFolder);
			var form = ShowDynamicDialog(dlg, true);
			if (((Form)form).DialogResult == DialogResult.OK)
			{
				var res = dlg.SelectedVersion;
				return (VersionSpec)res;
			}
			return null;
		}

		public static dynamic CreateDialogChooseWorkspace()
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogChooseWorkspace");
			dynamic dynamicDialog = ExposedObject.New(dialogType);
			var d = (Form)dynamicDialog;
			d.Load += (o, args) =>
			{
				var lv = (ListView)dynamicDialog.listViewWorkspaces;
				if (lv.Items.Count != TfsContext.GetWorkspaces().Count())
				{
					foreach (Workspace w in TfsContext.GetWorkspaces())
					{
						var listViewItem = new ListViewItem() { Tag = w, Selected = lv.Items.Count == 0 };
						dynamicDialog.workspaceToItem(w, listViewItem);
						lv.Items.Add(listViewItem);
					}
				}
			};

			return d;
		}

		public static Workspace ShowDialogChooseWorkspace()
		{
			dynamic dlg = CreateDialogChooseWorkspace();
			ShowDynamicDialog(dlg, true);
			var res = dlg.SelectedWorkspace;
			return res;
		}

		public static dynamic CreateDialogFindLabel()
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogFindLabel");
			dynamic dynamicDialog = ExposedObject.New(dialogType);
			dynamicDialog.VersionControlServer = TfsContext.VersionControlServer;
			dynamicDialog.AllowEditAndDeleteLabel = false;
			return dynamicDialog;
		}

		public static VersionControlLabel ShowDialogFindLabel()
		{
			dynamic d = CreateDialogFindLabel();
			ShowDynamicDialog(d, true);
			var res = d.SelectedLabel;
			return res;
		}

		public static dynamic CreateDialogFindShelveset(string userName = "")
		{
			if (string.IsNullOrEmpty(userName))
				userName = TfsContext.ConfigurationServer.AuthorizedIdentity.GetUniqueName();
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogFindShelveset");
			dynamic dynamicDialog = ExposedObject.New(dialogType, TfsContext.VersionControlServer, userName);
			return dynamicDialog;
		}

		public static Shelveset ShowDialogFindShelveset(string userName = "")
		{
			dynamic d = CreateDialogFindShelveset(userName);
			ShowDynamicDialog(d, true);
			var res = d.SelectedShelveset;
			return res;
		}

		public static object CreateFolderDiff(string path1, VersionSpec spec1, string path2, VersionSpec spec2, RecursionType recursion)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.FolderDiff");
			ExposedObject model = ExposedObject.New(dialogType, path1, spec1, path2, spec2, TfsContext.VersionControlServer, recursion);
			return model.Object;
		}

		public static dynamic CreateDialogFolderDiff(string path1, VersionSpec spec1, string path2, VersionSpec spec2, RecursionType recursion)
		{
			var model = CreateFolderDiff(path1, spec1, path2, spec2, recursion);
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogFolderDiff");
			dynamic dynamicDialog = ExposedObject.New(dialogType, model);
			return dynamicDialog;
		}

		public static Form ShowDialogFolderDiff(string path1, VersionSpec spec1, string path2,
			VersionSpec spec2, RecursionType recursion, bool modal = true)
		{
			dynamic d = CreateDialogFolderDiff(path1, spec1, path2, spec2, recursion);
			ShowDynamicDialog(d, modal);
			return (Form)d;
		}

		public static dynamic CreateDialogReconcile()
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogReconcile");
			dynamic dynamicDialog = ExposedObject.New(dialogType);
			return dynamicDialog;
		}

		public static Form ShowDialogReconcile(bool modal = true)
		{
			dynamic d = CreateDialogReconcile();
			ShowDynamicDialog(d, modal);
			return (Form)d;
		}


		public static dynamic CreateDialogWorkItemDetails(WorkItem workItem, bool allowModification)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.LocalPathLinkBox).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogWorkItemDetails");
			dynamic dynamicDialog = ExposedObject.New(dialogType, workItem, allowModification);
			return dynamicDialog;
		}

		public static Form ShowDialogWorkItemDetails(WorkItem workItem, bool allowModification, bool modal = true)
		{
			dynamic d = CreateDialogWorkItemDetails(workItem, allowModification);
			((Form)d).ShowInTaskbar = true;
			((Form)d).MinimizeBox = true;
			ShowDynamicDialog(d, modal);
			return (Form)d;
		}

		public static Form ShowDialogAlertSettings(string domain, string projectName, WorkItemStore store, bool modal = true)
		{
			dynamic d = CreateDialogAlertSettings(domain, projectName, store);
			ShowDynamicDialog(d, modal);
			return (Form)d;
		}

		public static object CreateDialogAlertSettings(string domain, string projectName, WorkItemStore store)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.WorkItemTracking.Controls.PickWorkItemsControl).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.WorkItemTracking.Controls.AlertSettings");
			dynamic dynamicDialog = ExposedObject.New(dialogType, domain, projectName, store);

			return dynamicDialog;
		}



		public static object CreateDialogSetLocalFolder(Workspace workspace, string serverPath)
		{
			Assembly assembly = typeof(Microsoft.TeamFoundation.VersionControl.Controls.ConfigureWorkspaceGrid).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.VersionControl.Controls.DialogSetLocalFolder");
			dynamic dynamicDialog = ExposedObject.New(dialogType, workspace, serverPath);

			return dynamicDialog;
		}



		public static Form ShowDialogSaveQueryAs(QueryDefinition query)
		{
			return ShowDialogSaveQueryAs(query.Parent, query.QueryText, query.Name);
		}

		public static Form ShowDialogSaveQueryAs(QueryFolder queryFolder, string query, string defaultQueryName)
		{
			dynamic d = CreateDialogSaveQueryAs(queryFolder, defaultQueryName);
			ShowDynamicDialog(d, true);
			if (((Form)d).DialogResult == DialogResult.OK)
			{
				if (d.SaveType.ToString() == "SaveAsServerQuery")
				{
					var definition = new QueryDefinition(d.QueryName, query, d.ParentFolder);
					//d.ParentFolder.Add(new QueryDefinition(d.QueryName, query));
					if (definition.IsNew)
					{
						definition.Parent.Add(definition);
						definition.Project.QueryHierarchy.Save();
					}
					else
					{
						throw new NotSupportedException("Changing of existing Query is currently not supported");
					}
					MessageBox.Show("Query is saved to: $" + d.ParentFolder.Path + @"\" + d.QueryName);
				}
				else
				{
					File.WriteAllText(d.FilePath, query);
					MessageBox.Show("Query is saved to: " + d.FilePath);
				}
			}
			return (Form)d;
		}

		public static object CreateDialogSaveQueryAs(QueryFolder queryFolder = null, string defaultQueryName = null)
		{
			//queryFolder = new QueryFolder("My Bugs");
			//defaultQueryName = "New Query";
			Assembly assembly = typeof(Microsoft.TeamFoundation.WorkItemTracking.Controls.PickWorkItemsControl).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.WorkItemTracking.Controls.QuerySaveAsDialog");
			dynamic dynamicDialog = queryFolder != null ? ExposedObject.New(dialogType, queryFolder, defaultQueryName) : ExposedObject.New(dialogType);

			return dynamicDialog;
		}

		//public static void AddPossibleWorkitemTypeToMenuItem(ToolStripMenuItem menuItem, Action<WorkItemType> onClickAction, string preText = "", int maxEntries = 0)
		//{
		//	int i = 1;
		//	foreach (var itemType in CheckoutAndBuildLogic.Instance.CurrentProject.WorkItemTypes.Cast<WorkItemType>())
		//	{
		//		if (maxEntries > 0 && i > maxEntries)
		//			break;
		//		var item = new ToolStripMenuItem(string.Format("{2}{1} {0}...", itemType.Name, preText, maxEntries > 0 ? i + " " : ""))
		//		{
		//			ToolTipText = itemType.Description,
		//			Tag = itemType
		//		};
		//		WorkItemType type = itemType;
		//		item.Click += (sender, args) => onClickAction(type);
		//		menuItem.DropDownItems.Add(item);
		//		i++;
		//	}
		//}

		public static QueryItem ShowDialogQueryPicker(Project[] projects, QueryPickerType pickerType)
		{
			return ShowDialogQueryPicker(projects, LastQueryItem, pickerType);
		}


		public static dynamic CreateQueryPickerTreeControl(Microsoft.TeamFoundation.WorkItemTracking.Client.Project[] projects, QueryItem selectedItem, QueryPickerType pickerType)
		{			
			//Assembly assembly = Assembly.LoadFrom(@"C:\Windows\assembly\GAC_MSIL\Microsoft.TeamFoundation.Common.Library\10.0.0.0__b03f5f7f11d50a3a\Microsoft.TeamFoundation.Common.Library.dll");
			Assembly assembly = typeof(Microsoft.TeamFoundation.WorkItemTracking.Controls.ColumnsPickerControl).Assembly;
			var controlType = assembly.GetType("Microsoft.TeamFoundation.WorkItemTracking.Controls.QueryPickerTreeControl");
			dynamic result = ExposedObject.New(controlType);
			result.Initialize(projects, selectedItem, pickerType);
			return result;
		}

		public static dynamic CreateDialogQueryPicker(Microsoft.TeamFoundation.WorkItemTracking.Client.Project[] projects, QueryItem selectedItem, QueryPickerType pickerType)
		{
			
			//Assembly assembly = Assembly.LoadFrom(@"C:\Windows\assembly\GAC_MSIL\Microsoft.TeamFoundation.Common.Library\10.0.0.0__b03f5f7f11d50a3a\Microsoft.TeamFoundation.Common.Library.dll");
			Assembly assembly = typeof(Microsoft.TeamFoundation.WorkItemTracking.Controls.ColumnsPickerControl).Assembly;
			var dialogType = assembly.GetType("Microsoft.TeamFoundation.WorkItemTracking.Controls.QueryPickerDialog");
			dynamic dynamicDialog = ExposedObject.New(dialogType, projects, selectedItem, pickerType);
			return dynamicDialog;
		}


		public static QueryItem ShowDialogQueryPicker(Microsoft.TeamFoundation.WorkItemTracking.Client.Project[] projects, QueryItem selectedItem, QueryPickerType pickerType)
		{
			var dlg = CreateDialogQueryPicker(projects, selectedItem, pickerType);
			var form = ShowDynamicDialog(dlg, true);
			if (((Form)form).DialogResult == DialogResult.OK)
			{
				var res = dlg.SelectedItem;
				LastQueryItem = (QueryItem)res;
				return (QueryItem)res;
			}
			return null;
		}

		//public static void ShowBuildDetailsDialog(IBuildDetail buildDetail)
		//{
		//	dynamic panelBuildDetailView = CreatePanelBuildDetailView(buildDetail);
		//	var r = (System.Windows.Controls.UserControl)panelBuildDetailView;
		//	var window = new System.Windows.Window();
		//	window.Content = r;
		//	window.Title = buildDetail.LabelName ?? buildDetail.BuildNumber;
		//	window.Show();
		//}


		public enum QueryPickerType
		{
			PickQuery,
			PickFolder,
			PickQueryOrFolder,
		}


		//public static dynamic CreatePanelBuildDetailView(IBuildDetail buildDetail)
		//{
		//	Assembly assembly = typeof(Microsoft.TeamFoundation.Build.Controls.ControlBuildDefinitionDefaultsTab).Assembly;
		//	var dialogType = assembly.GetType("Microsoft.TeamFoundation.Build.Controls.PanelBuildDetailView");
		//	dynamic dynamicDialog = ExposedObject.New(dialogType);
		//	dynamicDialog.Build = buildDetail;
		//	//dynamicDialog.RefreshDataContext(buildDetail);
		//	return dynamicDialog;
		//}

		//public static dynamic CreateControlBuildDefinition(IBuildDefinition buildDefinition, List<IBuildController> buildControllers, ControlBuildDefinitionTab tab)
		//{
		//	//Assembly assembly = Assembly.LoadFrom(@"C:\Windows\assembly\GAC_MSIL\Microsoft.TeamFoundation.Common.Library\10.0.0.0__b03f5f7f11d50a3a\Microsoft.TeamFoundation.Common.Library.dll");
		//	Assembly assembly = typeof(Microsoft.TeamFoundation.Build.Controls.ControlBuildDefinitionContinuousIntegrationTab).Assembly;
		//	var dialogType = assembly.GetType("Microsoft.TeamFoundation.Build.Controls.ControlBuildDefinition");
		//	VersionControlServer server = tfs.DefaultVersionControlServer;
		//	dynamic controlBuildDefinition = ExposedObject.New(dialogType, server, buildDefinition, buildControllers, tab);
		//	return controlBuildDefinition;
		//}

		public enum ControlBuildDefinitionTab
		{
			General,
			ContinuousIntegration,
			Workspace,
			BuildDefaults,
			Workflow,
			RetentionPolicy,
		}


	}
}