using System;
using System.Runtime.InteropServices;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Common.Internal;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.VersionControl.Common.Internal;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;
using OperationCanceledException = System.OperationCanceledException;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	internal static class VssProvider
	{
		private static IntPtr s_modalState = IntPtr.Zero;
		private static bool? s_isActive = true;
		private static IVersionControlProvider s_versionControlProvider;

		internal static bool IsActive
		{
			get { return s_isActive ?? false; }
			set { s_isActive = value; }
		}

		internal static bool IsSelectedProvider
		{
			get
			{
				//if (!VssProvider.s_isActive.HasValue)
				//	return ClientHelperVS.IsCmdUIContextActive(new Guid("{4CA58AB2-18FA-4F8D-95D4-32DDF27D184C}"));
				return s_isActive.Value;
			}
		}


		internal static bool IsAnySolutionFileControlledByHatteras
		{
			get
			{
				var isControlled = false;
				if (IsActive)
					HandleComReturn(GetProvider().IsAnySolutionFileControlledByHatteras(out isControlled));
				return isControlled;
			}
		}

		private static IVersionControlProvider GetProvider()
		{
			if (IsActive && s_versionControlProvider == null)
				s_versionControlProvider = Package.GetGlobalService(typeof (IVersionControlProvider)) as IVersionControlProvider;
			if (s_versionControlProvider == null)
				throw new Exception(Resources.Get("VssProviderInterOpUnavailable"));
			return s_versionControlProvider;
		}

		private static void HandleComReturn(int hr)
		{
			if (!HResult.Failed(hr))
				return;
			if (hr == -2147213311)
				throw new OperationCanceledException();
			var exceptionForHr = Marshal.GetExceptionForHR(hr);
			throw new Exception(exceptionForHr.Message, exceptionForHr);
		}


		internal static bool IsSolutionControlledByHatteras(string solutionFile)
		{
			var isControlled = false;
			if (IsActive)
			{
				HandleComReturn(GetProvider().IsSolutionControlledByHatteras(solutionFile, out isControlled));
				TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration,
					"VssProvider.IsSolutionControlledByHatteras; solutionFile={0} isControlled={1}", (object) solutionFile,
					(object) (isControlled ? 1 : 0));
			}
			return isControlled;
		}

		internal static bool IsFolderPartOfSolution(string fullFolderPath)
		{
			var isPartOfSolution = false;
			if (IsActive)
			{
				HandleComReturn(GetProvider().IsFolderPartOfSolution(fullFolderPath, out isPartOfSolution));
				TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration,
					"VssProvider.IsFolderPartOfSolution; fullFolderPath={0} isControlled={1}", (object) fullFolderPath,
					(object) (isPartOfSolution ? 1 : 0));
			}
			return isPartOfSolution;
		}

		internal static void OpenFromSCC(string solutionFile, string localDir, string version,
			VersionControlOpenFromSccOverwrite overwriteMode)
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration,
				"VssProvider.OpenFromSCC starts; solutionFile={0} localDir={1} version={2} overwriteMode={3}", (object) solutionFile,
				(object) localDir, (object) version, (object) overwriteMode);
			HandleComReturn(GetProvider().OpenFromSCC(solutionFile, localDir, version, overwriteMode));
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.OpenFromSCC ends");
		}

		//internal static void Undo(Workspace workspace, string[] fileNames, int flags)
		//{
		//	if (VssProvider.IsActive && SolutionManager.Instance.ShouldProcessWorkspace(workspace))
		//	{
		//		TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.Undo starts; fileNames={0} flags={1}", (object)fileNames, (object)flags);
		//		VssProvider.HandleComReturn(VssProvider.GetProvider().Undo(fileNames, flags));
		//		TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.Undo ends");
		//	}
		//	else
		//		ClientHelperVS.Undo(workspace, fileNames, false);
		//}

		internal static void ProcessUndoneDeletes(string[] fileNames)
		{
			if (!IsActive || fileNames == null || fileNames.Length == 0)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration,
				"VssProvider.ProcessUndoneDeletes starts; fileNames={0}{1}", (object) fileNames, (object) string.Empty);
			HandleComReturn(GetProvider().ProcessUndoneDeletes(fileNames));
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.ProcessUndoneDeletes ends");
		}

		internal static void ProcessUndoneUndeletes(string[] fileNames)
		{
			if (!IsActive || fileNames == null || fileNames.Length == 0)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration,
				"VssProvider.ProcessUndoneUndeletes starts; fileNames={0}{1}", (object) fileNames, (object) string.Empty);
			HandleComReturn(GetProvider().ProcessUndoneUndeletes(fileNames));
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.ProcessUndoneUndeletes ends");
		}

		internal static void ProcessUndoneAdds(string[] fileNames)
		{
			if (!IsActive || fileNames == null || fileNames.Length == 0)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration,
				"VssProvider.ProcessUndoneAdds starts; fileNames={0}{1}", (object) fileNames, (object) string.Empty);
			HandleComReturn(GetProvider().ProcessUndoneAdds(fileNames));
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.ProcessUndoneAdds ends");
		}

		internal static void RefreshGlyphs(string[] fileNames, bool immediate = false)
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration,
				"VssProvider.RefreshGlyphs starts; fileNames={0}{1}", (object) fileNames, (object) string.Empty);
			HandleComReturn(GetProvider().RefreshGlyphs(fileNames, immediate));
			TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration, "VssProvider.RefreshGlyphs ends");
		}

		//internal static void RefreshStatus()
		//{
		//	if (!VssProvider.IsActive)
		//		return;
		//	TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.RefreshStatus starts");
		//	SolutionManager.Instance.ClearAllStatusCaches();
		//	VssProvider.HandleComReturn(VssProvider.GetProvider().RefreshStatus());
		//	SolutionManager.Instance.RefreshStatusOfOpenedDocuments();
		//	TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.RefreshStatus ends");
		//}

		internal static void GetSelectedItemsList(VersionControlCommand command, out string[] fileNames)
		{
			fileNames = new string[0];
			if (!IsActive)
				return;
			HandleComReturn(GetProvider().GetSelectedItemsList(command, out fileNames));
			TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration, "VssProvider.GetSelectedItemsList");
			if (fileNames != null)
				return;
			fileNames = new string[0];
		}

		//internal static void GetSelectedItemsList2(bool useCurrentSelection, out SolutionHierarchyDiag solutionHierarchy)
		//{
		//	solutionHierarchy = (SolutionHierarchyDiag)null;
		//	if (!VssProvider.IsActive)
		//		return;
		//	string[] fileNames;
		//	int numberOfServerItems;
		//	int numberOfCodeBehindItems;
		//	int[] masterFileIndexes;
		//	string[] explicitFileNames;
		//	VssProvider.HandleComReturn(VssProvider.GetProvider().GetSelectedItemsList2(useCurrentSelection, out fileNames, out numberOfServerItems, out numberOfCodeBehindItems, out masterFileIndexes, out explicitFileNames));
		//	solutionHierarchy = SolutionHierarchyDiag.Create(!useCurrentSelection, fileNames, explicitFileNames, numberOfServerItems, numberOfCodeBehindItems, masterFileIndexes);
		//	TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration, "VssProvider.GetSelectedItemsList2");
		//}

		internal static void Shelve()
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.Shelve starts");
			HandleComReturn(GetProvider().Shelve());
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.Shelve ends");
		}

		internal static void AnnotateFiles()
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.AnnotateFiles starts");
			HandleComReturn(GetProvider().AnnotateFiles());
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.AnnotateFiles ends");
		}

		internal static void QueryCommandStatus(VersionControlCommand command, out bool supported, out bool enabled,
			out bool invisible)
		{
			supported = IsActive;
			enabled = IsActive;
			invisible = !IsActive;
			var pFallback = true;
			VersionControlOperationType operation;
			switch (command)
			{
				case VersionControlCommand.scc_command_lock:
					operation = VersionControlOperationType.Operation_Lock;
					break;
				case VersionControlCommand.scc_command_unlock:
					operation = VersionControlOperationType.Operation_Unlock;
					break;
				case VersionControlCommand.scc_command_annotate:
					operation = VersionControlOperationType.Operation_Annotate;
					break;
				default:
					operation = VersionControlOperationType.Operation_None;
					break;
			}
			if (operation != VersionControlOperationType.Operation_None)
			{
				var pHide = false;
				var pEnable = false;
				//SolutionManager.Instance.QueryCommandStatusForUnboundFile(operation, out pHide, out pEnable, out pFallback);
				if (!pFallback)
				{
					supported = true;
					invisible = pHide;
					enabled = pEnable;
				}
			}
			if (!pFallback || !IsActive)
				return;
			HandleComReturn(GetProvider().QueryCommandStatus(command, out supported, out enabled, out invisible));
		}

		internal static void RefreshProject(IVsHierarchy projectHierarchy)
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration, "VssProvider.RefreshProject starts");
			HandleComReturn(GetProvider().RefreshProject(projectHierarchy));
			TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration, "VssProvider.RefreshProject ends");
		}

		internal static void EnsureProviderIsActive()
		{
			if (s_versionControlProvider == null)
				s_versionControlProvider = Package.GetGlobalService(typeof (IVersionControlProvider)) as IVersionControlProvider;
			var registerScciProvider = Package.GetGlobalService(typeof (IVsRegisterScciProvider)) as IVsRegisterScciProvider;
			if (registerScciProvider == null)
				return;
			registerScciProvider.RegisterSourceControlProvider(new Guid("{4CA58AB2-18FA-4F8D-95D4-32DDF27D184C}"));
		}

		internal static bool IsProviderAvailable()
		{
			try
			{
				GetProvider();
			}
			catch{}
			return s_versionControlProvider != null;
		}


		internal static bool IsHintFile(string file)
		{
			var isHintFileResult = false;
			if (IsActive && !string.IsNullOrEmpty(file))
				HandleComReturn(GetProvider().IsHintFile(file, out isHintFileResult));
			return isHintFileResult;
		}

		internal static void UnbindAll()
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.UnbindAll starts");
			HandleComReturn(GetProvider().UnbindAll());
			TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.UnbindAll ends");
		}

		internal static bool IsFileBoundToSCC(string file)
		{
			var isBoundToSCC = false;
			if (IsActive && !string.IsNullOrEmpty(file))
				HandleComReturn(GetProvider().IsFileBoundToSCC(file, out isBoundToSCC));
			return isBoundToSCC;
		}

		internal static bool Get(string[] localItems, VersionSpec versionSpec, VersionControlGetFlags options)
		{
			var flag = false;
			if (IsActive)
			{
				TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.Get starts");
				HandleComReturn(GetProvider().Get(localItems, versionSpec.DisplayString, (int) options));
				TeamFoundationTrace.Info(VCTraceKeywordSets.SolutionIntegration, "VssProvider.Get ends");
				flag = true;
			}
			return flag;
		}

		internal static void SaveItems(string[] fileNames)
		{
			if (!IsActive)
				return;
			TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration, "VssProvider.SaveItems;  fileNames={0}{1}",
				(object) fileNames, (object) string.Empty);
			HandleComReturn(GetProvider().SaveItems(fileNames));
		}

		internal static string[] GetExcludedItemsForProject(string projectFile)
		{
			string[] fileNames = null;
			if (IsActive)
			{
				TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration,
					"VssProvider.GetExcludedItemsForProject starts;  projectFile={0}", (object) projectFile);
				HandleComReturn(GetProvider().GetExcludedItemsForProject(projectFile, out fileNames));
				TeamFoundationTrace.Verbose(VCTraceKeywordSets.SolutionIntegration,
					"VssProvider.GetExcludedItemsForProject ends;  fileNames={0}{1}", (object) fileNames, (object) string.Empty);
			}
			return fileNames ?? new string[0];
		}

		internal static bool IsProjectControlledByHatteras(string projectFile)
		{
			var isControlled = false;
			if (IsActive)
				HandleComReturn(GetProvider().IsProjectControlledByHatteras(projectFile, out isControlled));
			return isControlled;
		}
	}
}