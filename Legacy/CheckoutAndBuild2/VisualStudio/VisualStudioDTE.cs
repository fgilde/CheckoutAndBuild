using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Common.Internal;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.VersionControl.Controls;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;
using Microsoft.Win32;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Solution = EnvDTE.Solution;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	public static class VisualStudioDTE
	{
	    public static bool IsBusy;

		public static DTE2 Instance => CheckoutAndBuild2Package.GetGlobalService<DTE2>();

	    static VisualStudioDTE()
	    {
            Instance.Events.SolutionEvents.BeforeClosing += SolutionEventsOnBeforeClosing;
            Instance.Events.SolutionEvents.AfterClosing += SolutionEventsOnAfterClosing;
            Instance.Events.SolutionEvents.Opened += SolutionEventsOnOpened;            
	    }

	    private static void SolutionEventsOnOpened()
	    {
	        IsBusy = false;
	    }

	    private static void SolutionEventsOnAfterClosing()
	    {
            IsBusy = false;
        }

	    private static void SolutionEventsOnBeforeClosing()
	    {
            IsBusy = true;
        }

	    public static ITrackSelection GetITrackSelection(Guid toolWindowId)
		{	
			ServiceProvider frameSP = GetShellServiceProviderFromOtherWindow(toolWindowId);
			var trackSelection = frameSP.GetService(typeof(STrackSelection)) as ITrackSelection;
			return trackSelection;
		}

		public static ServiceProvider GetShellServiceProviderFromOtherWindow(Guid toolWindowId)
		{
			var frame = GetToolWindowFrame(toolWindowId);
			object objFrameSP;
			frame.GetProperty((int)__VSFPROPID.VSFPROPID_SPFrame, out objFrameSP);
			ServiceProvider frameSP = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)objFrameSP);
			return frameSP;
		}

		public static IVsWindowFrame GetToolWindowFrame(Guid toolWindowId)
		{
			var shell = CheckoutAndBuild2Package.GetGlobalService<IVsUIShell>();
			IVsWindowFrame testResultsFrame;
			var rguidPersistenceSlot = toolWindowId;
			shell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref rguidPersistenceSlot, out testResultsFrame);
			return testResultsFrame;
		}

		public static void TryOpenFile(string fileName, int lineNumber, SimpleVsHierarchy hierarchyItem)
		{
			var toOpen = fileName;
			if (!File.Exists(toOpen) && hierarchyItem != null)
			{
				var directoryName = Path.GetDirectoryName(hierarchyItem.ProjectFile);
				if (directoryName != null)
				{
					toOpen = Path.Combine(directoryName, fileName);
					if (!File.Exists(toOpen))
						toOpen = hierarchyItem.ProjectFile;
				}
			}
			TryOpenFile(toOpen, lineNumber);
		}

		public static void TryOpenFile(string file, int lineNumber = 0)
		{
			if (File.Exists(file))
			{
				Instance.ItemOperations.OpenFile(file);

				if (lineNumber > 0 && Instance.ActiveDocument != null)
				{
					var selection = Instance.ActiveDocument.Selection as TextSelection;
					if (selection != null) selection.GotoLine(lineNumber);
				}
			}
		}

		public static void ViewPendingChanges(Workspace workspace, IEnumerable<PendingChange> changes)
		{
			using (UIHost.GetWaitCursor())
			{
				foreach (PendingChange pendingChange in changes)
				{
					if (pendingChange.ItemType == ItemType.File)
					{
						if ((pendingChange.ServerItem).EndsWith(".sln"))
						{
							TeamFoundationTrace.Info(VCTraceKeywordSets.PendingChanges, "View(VS):Opening solution/project '{0}'", (object)pendingChange.ServerItem);
							OpenSolutionWithWorkspace(workspace, pendingChange.ServerItem, VersionSpec.Latest);
						}
						else
						{
							var str = VersionControlPath.GetTempFileName(pendingChange.ServerItem, pendingChange.Version);
							pendingChange.DownloadBaseFile(str);
							TryOpenFile(str);
						}
					}
				}
			}
		}

		internal static DialogResult Error(IWin32Window owner, string text, string helpTopic, MessageBoxButtons buttons, MessageBoxIcon icon)
		{
			return UIHost.ShowMessageBox(owner, text, helpTopic, "Error", buttons, icon, MessageBoxDefaultButton.Button1);
		}

		internal static void OpenSolutionWithWorkspace(Workspace workspace, string serverItem, VersionSpec spec)
		{
			serverItem = VersionControlPath.GetFullPath(serverItem);
			WorkingFolder folderForServerItem1;
			try
			{
				folderForServerItem1 = workspace.TryGetWorkingFolderForServerItem(serverItem);
			}
			catch (Exception ex)
			{
				Output.Exception(ex);
				return;
			}
			if (folderForServerItem1 != null)
			{
				if (folderForServerItem1.IsCloaked)
				{
					int num1 = (int)Error(UIHost.DefaultParentWindow, GuiResources.Format("SolutionIsCloaked", (object)VersionControlPath.GetFileName(serverItem)), string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Hand);
				}
				else
				{
					try
					{
						VssProvider.OpenFromSCC(serverItem, FileSpec.GetDirectoryName(folderForServerItem1.LocalItem), spec.DisplayString, VersionControlOpenFromSccOverwrite.openscc_open_local_version);
					}
					catch (Exception ex)
					{
						Output.Exception(ex);
					}
				}
			}
			else
			{
				string folderName = VersionControlPath.GetFolderName(serverItem);
				using (var dialogSetLocalFolder = (Form)TeamControlFactory.CreateDialogSetLocalFolder(workspace, folderName))
				{
					if (UIHost.ShowModalDialog((Form)dialogSetLocalFolder) != DialogResult.OK)
						return;
				}
				try
				{
					WorkingFolder folderForServerItem2 = workspace.GetWorkingFolderForServerItem(serverItem);
					VssProvider.OpenFromSCC(serverItem, FileSpec.GetDirectoryName(folderForServerItem2.LocalItem), spec.DisplayString, VersionControlOpenFromSccOverwrite.openscc_open_local_version);
				}
				catch (Exception ex)
				{
					Output.Exception(ex);
				}
			}
		}



		public static readonly Guid IID_IVsHierarchy = new Guid("{59B2D1D0-5DB0-4F9F-9609-13F0168516D6}");

		public static OleMenuCommand AddMenuCommand(IMenuCommandService menuService, CommandID commandId,
			EventHandler callback, EventHandler queryStatusCallback)
		{
			var oleMenuCommand = new OleMenuCommand(callback, commandId);
			if (queryStatusCallback != null)
				oleMenuCommand.BeforeQueryStatus += queryStatusCallback;
			menuService.AddCommand(oleMenuCommand);
			return oleMenuCommand;
		}

		//public static unsafe void SetCommandText(IntPtr data, string menuText)
		//{
		//	var olecmdtextPtr = (OLECMDTEXT*) (void*) data;
		//	if (((int) olecmdtextPtr->cmdtextf & 1) == 0)
		//		return;
		//	olecmdtextPtr->cwActual = (uint) menuText.Length;
		//	var length = Math.Min(menuText.Length, (int) olecmdtextPtr->cwBuf - 1);
		//	if (length <= 0)
		//		return;
		//	Marshal.Copy(menuText.ToCharArray(), 0, (IntPtr) &olecmdtextPtr->rgwz, length);
		//	*(short*) ((IntPtr) & olecmdtextPtr->rgwz + (IntPtr) length*2) = 0;
		//}

		public static object GetService(Type type)
		{
			return Package.GetGlobalService(type);
		}

		public static object GetService(IServiceProvider provider, Guid serviceGuid)
		{
			var riid = VSConstants.IID_IUnknown;
			IntPtr ppvObject;
			if (!HResult.Succeeded(provider.QueryService(ref serviceGuid, ref riid, out ppvObject)))
				return null;
			try
			{
				return Marshal.GetObjectForIUnknown(ppvObject);
			}
			finally
			{
				Marshal.Release(ppvObject);
			}
		}

		public static TfsTeamProjectCollection GetTfsTeamProjectCollection(IHostPlugin plugin)
		{
			return plugin.GetService<ITeamFoundationContextManager2>().CurrentContext.TeamProjectCollection;
		}

		public static Guid GetDteClsid(IHostPlugin serviceProvider)
		{
			try
			{
				var localRegistry4 = serviceProvider.GetService<SLocalRegistry>() as ILocalRegistry4;
				var pdwRegRootHandle = 0U;
				if (localRegistry4 != null)
				{
					string pbstrRoot;
					if (HResult.Succeeded(localRegistry4.GetLocalRegistryRootEx(2U, out pdwRegRootHandle, out pbstrRoot)))
					{
						switch (pdwRegRootHandle)
						{
							case 2147483649U:
								using (var registryKey = Registry.CurrentUser.OpenSubKey(pbstrRoot))
								{
									if (registryKey != null)
									{
										var obj = registryKey.GetValue("ThisVersionDTECLSID");
										if (obj is string)
										{
											Guid result;
											if (Guid.TryParse((string)obj, out result))
												return result;
										}
									}
									break;
								}
							case 2147483650U:
								using (var registryKey = Registry.LocalMachine.OpenSubKey(pbstrRoot))
								{
									if (registryKey != null)
									{
										var obj = registryKey.GetValue("ThisVersionDTECLSID");
										if (obj is string)
										{
											Guid result;
											if (Guid.TryParse((string)obj, out result))
												return result;
										}
									}
									break;
								}
						}
					}
				}
			}
			catch (Exception ex)
			{
				TeamFoundationTrace.TraceAndDebugFailException(ex);
			}
			return new Guid();
		}

		public static IVsWindowFrame LaunchInWebBrowser(IHostPlugin plugin, string url, bool newWindow)
		{
			var webBrowsingService = plugin.GetService<SVsWebBrowsingService>() as IVsWebBrowsingService;
			IVsWindowFrame ppFrame = null;
			if (webBrowsingService != null)
			{
				var vswbnavigateflags = __VSWBNAVIGATEFLAGS.VSNWB_ForceNew;
				if (!newWindow)
					vswbnavigateflags = __VSWBNAVIGATEFLAGS.VSNWB_AddToMRU;
				webBrowsingService.Navigate(url, (uint)vswbnavigateflags, out ppFrame);
			}
			return ppFrame;
		}

		public static IVsWindowFrame LaunchInWebBrowser(IHostPlugin plugin, string url, string caption, bool newWindow,
			out IVsWebBrowser browser)
		{
			var webBrowsingService = plugin.GetService<SVsWebBrowsingService>() as IVsWebBrowsingService;
			var vscreatewebbrowser = (__VSCREATEWEBBROWSER)1057;
			if (!newWindow)
				vscreatewebbrowser = __VSCREATEWEBBROWSER.VSCWB_AddToMRU;
			var rguidOwner = new Guid();
			IVsWindowFrame ppFrame = null;
			browser = null;
			if (webBrowsingService != null)
				webBrowsingService.CreateWebBrowser((uint)vscreatewebbrowser, ref rguidOwner, caption, url, null, out browser,
					out ppFrame);
			return ppFrame;
		}

		public static IVsTrackSelectionEx GetSelectionTrackingService(IVsWindowFrame windowFrame)
		{
			object pvar;
			windowFrame.GetProperty(-3002, out pvar);
			var provider = pvar as IServiceProvider;
			if (provider != null)
				return (IVsTrackSelectionEx)GetService(provider, typeof(SVsTrackSelectionEx).GUID);
			return null;
		}

		public static void PutInEditMode(IVsUIHierarchyWindow window, IVsUIHierarchy hierarchy, uint itemId)
		{
			window.ExpandItem(hierarchy, itemId, EXPANDFLAGS.EXPF_SelectItem);
			window.ExpandItem(hierarchy, itemId, EXPANDFLAGS.EXPF_EditItemLabel);
		}

		public static void HighlightCutItem(IVsUIHierarchyWindow window, IVsUIHierarchy hierarchy, uint itemId)
		{
			window.ExpandItem(hierarchy, itemId, EXPANDFLAGS.EXPF_AddCutHighlightItem);
		}

		public static void UnHighlightCutItem(IVsUIHierarchyWindow window, IVsUIHierarchy hierarchy, uint itemId)
		{
			window.ExpandItem(hierarchy, itemId, EXPANDFLAGS.EXPF_UnCutHighlightItem);
		}

		public static void SelectNode(IVsUIHierarchyWindow window, IVsUIHierarchy hierarchy, uint itemId)
		{
			window.ExpandItem(hierarchy, itemId, EXPANDFLAGS.EXPF_ExpandParentsToShowItem);
			window.ExpandItem(hierarchy, itemId, EXPANDFLAGS.EXPF_SelectItem);
		}

		public static void ExpandNode(IVsUIHierarchyWindow window, IVsUIHierarchy hierarchy, uint itemId, bool expand)
		{
			window.ExpandItem(hierarchy, itemId, expand ? EXPANDFLAGS.EXPF_ExpandFolder : EXPANDFLAGS.EXPF_CollapseFolder);
		}

		public static bool IsExpanded(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			if (!HResult.Succeeded(hierarchy.GetProperty(itemId, -2035, out pvar)))
				return false;
			return (bool)pvar;
		}

		public static string GetName(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			hierarchy.GetProperty(itemId, -2012, out pvar);
			return (string)pvar;
		}

		public static void SetName(IVsHierarchy hierarchy, uint itemId, string name)
		{
			hierarchy.SetProperty(itemId, -2012, name);
		}

		public static string GetCaption(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			hierarchy.GetProperty(itemId, -2003, out pvar);
			return (string)pvar;
		}

		public static void SetCaption(IVsHierarchy hierarchy, uint itemId, string caption)
		{
			hierarchy.SetProperty(itemId, -2003, caption);
		}

		public static uint GetParent(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			hierarchy.GetProperty(itemId, -1000, out pvar);
			if (pvar is int)
				return (uint)(int)pvar;
			return (uint)pvar;
		}

		public static uint GetNextSibling(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			hierarchy.GetProperty(itemId, -1002, out pvar);
			return (uint)pvar;
		}

		public static uint GetFirstChild(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			hierarchy.GetProperty(itemId, -1001, out pvar);
			return (uint)pvar;
		}

		public static IVsHierarchy GetNestedHierarchy(IVsHierarchy hierarchy, uint itemId, out uint nestedItemId)
		{
			var iidHierarchyNested = IID_IVsHierarchy;
			IntPtr ppHierarchyNested;
			var nestedHierarchy = hierarchy.GetNestedHierarchy(itemId, ref iidHierarchyNested, out ppHierarchyNested,
				out nestedItemId);
			IVsHierarchy vsHierarchy = null;
			try
			{
				if (HResult.Succeeded(nestedHierarchy))
				{
					if (ppHierarchyNested != IntPtr.Zero)
						vsHierarchy = (IVsHierarchy)Marshal.GetObjectForIUnknown(ppHierarchyNested);
				}
			}
			finally
			{
				TFCommonUtil.SafeRelease(ppHierarchyNested);
			}
			return vsHierarchy;
		}

		public static IVsHierarchy GetParentHierarchy(IVsHierarchy hierarchy)
		{
			object pvar;
			hierarchy.GetProperty(4294967294U, -2032, out pvar);
			var vsHierarchy = pvar as IVsHierarchy;
			if (vsHierarchy == null)
			{
				var unknownWrapper = pvar as UnknownWrapper;
				if (unknownWrapper != null)
					vsHierarchy = unknownWrapper.WrappedObject as IVsHierarchy;
			}
			return vsHierarchy;
		}

		public static IVsHierarchy GetHierarchyParent(IVsHierarchy hierarchy, out uint parentItemId)
		{
			parentItemId = uint.MaxValue;
			var parentHierarchy = GetParentHierarchy(hierarchy);
			if (parentHierarchy == null)
				return null;
			object pvar;
			hierarchy.GetProperty(4294967294U, -2033, out pvar);
			if (pvar == null)
				return null;
			parentItemId = Convert.ToUInt32(pvar, NumberFormatInfo.InvariantInfo);
			return parentHierarchy;
		}

		public static void SetHierarchyParent(IVsHierarchy hierarchy, IVsHierarchy parentHierarchy, uint parentItemId)
		{
			hierarchy.SetProperty(4294967294U, -2032, parentHierarchy);
			hierarchy.SetProperty(4294967294U, -2033, parentItemId);
		}

		public static bool QueryClose(IVsHierarchy hierarchy)
		{
			int pfCanClose;
			if (hierarchy.QueryClose(out pfCanClose) < 0)
				return false;
			return pfCanClose != 0;
		}

		public static IntPtr GetIcon(IVsHierarchy hierarchy, uint itemId, out bool deleteIcon)
		{
			deleteIcon = false;
			object pvar;
			hierarchy.GetProperty(itemId, -2005, out pvar);
			if (pvar == null)
			{
				hierarchy.GetProperty(itemId, -2013, out pvar);
				return TFCommonUtil.GetImageHandle(pvar);
			}
			var num = (uint)pvar;
			hierarchy.GetProperty(4294967294U, -2004, out pvar);
			var imageHandle = TFCommonUtil.GetImageHandle(pvar);
			if (imageHandle != IntPtr.Zero)
			{
				var imageCount = NativeMethods.ImageList_GetImageCount(imageHandle);
				if ((int)num < imageCount)
				{
					var icon = NativeMethods.ImageList_GetIcon(imageHandle, (int)num, 0U);
					if (icon != IntPtr.Zero)
					{
						deleteIcon = true;
						return icon;
					}
				}
			}
			return IntPtr.Zero;
		}

		public static bool IsDisplayed(IVsHierarchy hierarchy, uint itemId)
		{
			object pvar;
			do
			{
				hierarchy.GetProperty(itemId, -1000, out pvar);
				itemId = (uint)(int)pvar;
				if ((int)itemId == -1)
				{
					hierarchy = GetHierarchyParent(hierarchy, out itemId);
					if (hierarchy == null)
						return true;
				}
			} while ((int)itemId == -1 || HResult.Succeeded(hierarchy.GetProperty(itemId, -2035, out pvar)) && (bool)pvar);
			return false;
		}

		private static bool IsSameComObject(object obj1, object obj2)
		{
			var flag = false;
			var pUnk1 = IntPtr.Zero;
			var pUnk2 = IntPtr.Zero;
			try
			{
				if (obj1 != null)
				{
					if (obj2 != null)
					{
						pUnk1 = QueryInterfaceIUnknown(obj1);
						pUnk2 = QueryInterfaceIUnknown(obj2);
						flag = Equals(pUnk1, pUnk2);
					}
				}
			}
			finally
			{
				if (pUnk1 != IntPtr.Zero)
					Marshal.Release(pUnk1);
				if (pUnk2 != IntPtr.Zero)
					Marshal.Release(pUnk2);
			}
			return flag;
		}

		private static IntPtr QueryInterfaceIUnknown(object objToQuery)
		{
			var flag = false;
			var pUnk = IntPtr.Zero;
			IntPtr ppv;
			try
			{
				if (objToQuery is IntPtr)
				{
					pUnk = (IntPtr)objToQuery;
				}
				else
				{
					pUnk = Marshal.GetIUnknownForObject(objToQuery);
					flag = true;
				}
				var iid = VSConstants.IID_IUnknown;
				ErrorHandler.ThrowOnFailure(Marshal.QueryInterface(pUnk, ref iid, out ppv));
			}
			finally
			{
				if (flag && pUnk != IntPtr.Zero)
					Marshal.Release(pUnk);
			}
			return ppv;
		}

		public static bool IsSubItem(IVsHierarchy hierarchy, uint itemId, IVsHierarchy parentHierarchy, uint parentItemId)
		{
			if (hierarchy == null)
				throw new ArgumentNullException("hierarchy");
			if (parentHierarchy == null)
				throw new ArgumentNullException("parentHierarchy");
			while (!IsSameComObject(hierarchy, parentHierarchy))
			{
				if ((hierarchy = GetHierarchyParent(hierarchy, out itemId)) == null)
					return false;
			}
			for (; (int)itemId != (int)parentItemId; itemId = GetParent(hierarchy, itemId))
			{
				if ((int)itemId == -2 || (int)itemId == -1)
					return false;
			}
			return true;
		}

		public static void WaitOnePumping(WaitHandle waitHandle)
		{
			var num = 0U;
			while (!waitHandle.WaitOne(10, false))
			{
				if ((int)num == 0)
					num = (uint)NativeMethods.RegisterWindowMessage("WindowsForms12_ThreadCallbackMessage");
				NativeMethods.MSG msg;
				while (NativeMethods.PeekMessage(out msg, IntPtr.Zero, num, num, true))
					NativeMethods.DispatchMessage(ref msg);
			}
		}

		public static List<VSSAVETREEITEM> GetDirtyDocuments(IVsHierarchy parentHierarchy, uint parentItemId)
		{
			var items = new List<VSSAVETREEITEM>();
			ProcessRDTDocuments((rdtFlags, readLocks, editLocks, canonicalName, hierarchy, itemId, punkDocData) =>
			{
				var persistHierarchyItem = hierarchy as IVsPersistHierarchyItem;
				int pfDirty;
				if (persistHierarchyItem == null ||
					!HResult.Succeeded(persistHierarchyItem.IsItemDirty(itemId, punkDocData, out pfDirty)) ||
					(pfDirty == 0 || !IsSubItem(hierarchy, itemId, parentHierarchy, parentItemId)))
					return;
				items.Add(new VSSAVETREEITEM
				{
					grfSave = 1U,
					pHier = hierarchy,
					itemid = itemId
				});
			});
			return items;
		}

		public delegate void RDTDocumentsDelegate(
			uint rdtFlags, uint readLocks, uint editLocks, string canonicalName, IVsHierarchy hierarchy, uint itemId,
			IntPtr punkDocData);

		public static List<string> GetOpenFiles()
		{
			var items = new List<string>();
			ProcessRDTDocuments((rdtFlags, readLocks, editLocks, canonicalName, hierarchy, itemId, punkDocData) =>
			{
				try
				{
					if (string.IsNullOrEmpty(canonicalName) || !Path.IsPathRooted(canonicalName))
						return;
					items.Add(canonicalName);
				}
				catch (ArgumentException) { }
			});
			return items;
		}

		public static void ProcessRDTDocuments(RDTDocumentsDelegate processAction)
		{
			var runningDocumentTable = (IVsRunningDocumentTable)GetService(typeof(SVsRunningDocumentTable));
			IEnumRunningDocuments ppenum;
			runningDocumentTable.GetRunningDocumentsEnum(out ppenum);
			var rgelt = new uint[10];
			uint pceltFetched;
			while (HResult.Succeeded(ppenum.Next((uint)rgelt.Length, rgelt, out pceltFetched)) && pceltFetched > 0U)
			{
				for (var index = 0; (long)index < (long)pceltFetched; ++index)
				{
					uint pgrfRDTFlags;
					uint pdwReadLocks;
					uint pdwEditLocks;
					string pbstrMkDocument;
					IVsHierarchy ppHier;
					uint pitemid;
					IntPtr ppunkDocData;
					var documentInfo = runningDocumentTable.GetDocumentInfo(rgelt[index], out pgrfRDTFlags, out pdwReadLocks,
						out pdwEditLocks, out pbstrMkDocument, out ppHier, out pitemid, out ppunkDocData);
					try
					{
						if (HResult.Succeeded(documentInfo))
						{
							if (!(ppunkDocData == IntPtr.Zero))
								processAction(pgrfRDTFlags, pdwReadLocks, pdwEditLocks, pbstrMkDocument, ppHier, pitemid, ppunkDocData);
						}
					}
					finally
					{
						TFCommonUtil.SafeRelease(ppunkDocData);
					}
				}
			}
		}

		public static int PromptToSaveModifiedFiles(IVsHierarchy parentHierarchy, uint parentItemId)
		{
			var dirtyDocuments = GetDirtyDocuments(parentHierarchy, parentItemId);
			var num = 0;
			if (dirtyDocuments.Count > 0)
			{
				var rgSaveItems = dirtyDocuments.ToArray();
				//num = (VsipHost.GetService(typeof(SVsUIShell)) as IVsUIShell2).SaveItemsViaDlg((uint)rgSaveItems.Length, rgSaveItems);
				num = (GetService(typeof(SVsUIShell)) as IVsUIShell2).SaveItemsViaDlg((uint)rgSaveItems.Length, rgSaveItems);
			}
			return num;
		}

		public static int PromptToSaveModifiedSolutionFiles()
		{
			return PromptToSaveModifiedFiles(GetService(typeof(SVsSolution)) as IVsHierarchy, 4294967294U);
		}

		public static void ShowContextMenu(Guid menuIdGuid, int menuId, Point screenPosition, CommandHandler commandHandler)
		{
			var vsUiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
			if (vsUiShell == null)
				return;
			var pos = new[]
			{
				new POINTS()
			};
			pos[0].x = (short)screenPosition.X;
			pos[0].y = (short)screenPosition.Y;
			var rclsidActive = menuIdGuid;
			vsUiShell.ShowContextMenu(0U, ref rclsidActive, menuId, pos, commandHandler);
		}

		public static bool PromptToStopDebugging()
		{
			var vsDebugger2 = ServiceProvider.GlobalProvider.GetService(typeof(SVsShellDebugger)) as IVsDebugger2;
			return vsDebugger2 == null || vsDebugger2.ConfirmStopDebugging("Stop Debugging") == 0;
		}

		public static string[] SelectMultipleFilesUsingDialog(IntPtr handle, string title, string filter, string path,
			bool multiselect, string helpTopic)
		{
			var vsUiShell2 = GetService(typeof(SVsUIShell)) as IVsUIShell2;
			if (vsUiShell2 == null)
				return null;
			if (filter != null)
				filter = filter.Replace('|', char.MinValue) + char.MinValue;
			var vsopenfilenamew = new VSOPENFILENAMEW();
			string[] strArray = null;
			var num1 = IntPtr.Zero;
			const int num2 = 16384;
			const int num3 = 2 * num2;
			try
			{
				num1 = Marshal.AllocCoTaskMem(num3);
				NativeMethods.ZeroMemory(num1, num3);
				vsopenfilenamew.lStructSize = (uint)Marshal.SizeOf(typeof(VSOPENFILENAMEW));
				vsopenfilenamew.hwndOwner = handle;
				vsopenfilenamew.pwzFilter = filter;
				vsopenfilenamew.pwzDlgTitle = title;
				vsopenfilenamew.pwzFileName = num1;
				vsopenfilenamew.nMaxFileName = num2;
				vsopenfilenamew.pwzInitialDir = path;
				vsopenfilenamew.dwFlags = 8U;
				if (multiselect)
					vsopenfilenamew.dwFlags |= 512U;
				var pOpenFileName = new[]
				{
					vsopenfilenamew
				};
				if (vsUiShell2.GetOpenFileNameViaDlgEx(pOpenFileName, helpTopic) == 0)
					strArray = GetSelectedFiles(num1, multiselect);
			}
			finally
			{
				if (num1 != IntPtr.Zero)
					Marshal.FreeCoTaskMem(num1);
			}
			return strArray;
		}

		private static unsafe string[] GetSelectedFiles(IntPtr buffPtr, bool multiSelect)
		{
			var list1 = new List<string>();
			if (!multiSelect)
			{
				var str = Marshal.PtrToStringAuto(buffPtr);
				list1.Add(str);
			}
			else
			{
				var chPtr = (char*)buffPtr.ToPointer();
				while (*chPtr != 0)
				{
					var str = Marshal.PtrToStringAuto(new IntPtr(chPtr));
					list1.Add(str);
					if (str != null) chPtr += str.Length + 1;
				}
			}
			var list2 = new List<string>(list1.Count);
			if (list1.Count == 1)
			{
				list2.Add(list1[0]);
			}
			else
			{
				for (var index = 1; index < list1.Count; ++index)
				{
					var str = Path.Combine(list1[0], list1[index]);
					list2.Add(str);
				}
			}
			return list2.ToArray();
		}

		public static string SelectDirectoryUsingDialog(IntPtr handle, string title, string startFolder, string helpTopic,
			string openButtonLabel)
		{
			var vsUiShell2 = GetService(typeof(SVsUIShell)) as IVsUIShell2;
			if (vsUiShell2 == null)
				return null;
			var vsbrowseinfow = new VSBROWSEINFOW();
			var ptr = Marshal.AllocCoTaskMem(2048);
			vsbrowseinfow.lStructSize = (uint)Marshal.SizeOf(typeof(VSBROWSEINFOW));
			vsbrowseinfow.hwndOwner = handle;
			vsbrowseinfow.pwzDirName = ptr;
			vsbrowseinfow.nMaxDirName = 1024U;
			vsbrowseinfow.pwzDlgTitle = title;
			vsbrowseinfow.pwzInitialDir = startFolder;
			vsbrowseinfow.dwFlags = 1U;
			string str = null;
			try
			{
				if (vsUiShell2.GetDirectoryViaBrowseDlgEx(new[]
				{
					vsbrowseinfow
				}, helpTopic, openButtonLabel, null, null) == 0)
					str = Marshal.PtrToStringAuto(ptr);
			}
			finally
			{
				if (ptr != IntPtr.Zero)
					Marshal.FreeCoTaskMem(ptr);
			}
			return str;
		}



	}
}