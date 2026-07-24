using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using FG.CheckoutAndBuild2.Common;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	internal static class PackageHelper
	{
		public static Guid CommandSetGuid = new Guid("{2dc8d6bb-916c-4b80-9c52-fd8fc371acc2}");
		public static TfsContext TfsContext { get { return CheckoutAndBuild2Package.GetGlobalService<TfsContext>(); } }
		public static IVsUIShell VsUIShell { get { return CheckoutAndBuild2Package.GetGlobalService<IVsUIShell>(); } }

		public static object GetTestResultsToolWindowInstance()
		{
			var vsPackage = PackageHelper.GetPackage(VisualStudioIds.TestManagmentPackageId.ToGuid());
			if (vsPackage != null)
			{
				PropertyInfo toolWinProperty = vsPackage.GetType().GetProperty("ResultsToolWindowInstance", BindingFlags.Instance | BindingFlags.NonPublic);
				if (toolWinProperty != null)
				{
					return toolWinProperty.GetValue(vsPackage);				
				}
			}
			return null;
		}

		public static object GetTestResultsToolWindowContextHelperInstance()
		{
			return Check.TryCatch<object, Exception>(() =>
			{
				var resultToolWindowInstance = GetTestResultsToolWindowInstance();
				if (resultToolWindowInstance != null)
				{
					object mContext = ((dynamic)ExposedObject.From(resultToolWindowInstance)).m_context as object;
					if (mContext != null)
					{
						return mContext;
					}
				}
				return null;				
			});
		}

		public static IVsPackage GetPackage(Guid packageId)
		{
			var shell = CheckoutAndBuild2Package.GetGlobalService<IVsShell>();			
			Guid testManageMentPackageId = packageId;
			int isInstalled	;
			shell.IsPackageInstalled(testManageMentPackageId, out isInstalled);
			if (isInstalled != 1)
				return null;
			IVsPackage res;
			shell.LoadPackage(packageId, out res);
			return res;
		}

		public static void ShowContextMenu(Control control, int menuId, MouseEventArgs mouseArg, IOleCommandTarget commandTarget)
		{
			try
			{
				POINTS[] p = new POINTS[1]
        {
          new POINTS()
        };
				p[0].x = (short)mouseArg.X;
				p[0].y = (short)mouseArg.Y;
				Action action = (Action)(() => ShowContextMenuInternal(menuId, p, commandTarget));
				control.BeginInvoke((Delegate)action);
			}
			catch (Exception ex)
			{
				int num = (int)UIHost.ShowException(ex);
			}
		}

		public static void ShowContextMenu(System.Windows.Controls.Control control, int menuId, IOleCommandTarget commandTarget)
		{
			try
			{
				Point point = control.PointToScreen(Mouse.GetPosition((IInputElement)control));
				POINTS[] p = new POINTS[1]
        {
          new POINTS()
        };
				p[0].x = (short)point.X;
				p[0].y = (short)point.Y;
				Action action = (Action)(() => ShowContextMenuInternal(menuId, p, commandTarget));
				control.Dispatcher.BeginInvoke((Delegate)action);
			}
			catch (Exception ex)
			{
				int num = (int)UIHost.ShowException(ex);
			}
		}

		public static void ShowContextMenu(System.Windows.Controls.Control control, int menuId, Point point, IOleCommandTarget commandTarget)
		{
			try
			{
				POINTS[] p = new POINTS[1]
        {
          new POINTS()
        };
				p[0].x = (short)point.X;
				p[0].y = (short)point.Y;
				Action action = (Action)(() => ShowContextMenuInternal(menuId, p, commandTarget));
				control.Dispatcher.BeginInvoke((Delegate)action);
			}
			catch (Exception ex)
			{
				int num = (int)UIHost.ShowException(ex);
			}
		}

		private static void ShowContextMenuInternal(int menuId, POINTS[] p, IOleCommandTarget commandTarget)
		{
			
			try
			{
				VsUIShell.ShowContextMenu(uint.MaxValue, ref CommandSetGuid, menuId, p, commandTarget);
			}
			catch (Exception ex)
			{
				int num = (int)UIHost.ShowException(ex);
			}
		}

		public static bool IsVsAccelerator(Message msg)
		{
			switch ((Keys)(msg.WParam.ToInt32() & (int)ushort.MaxValue) | Control.ModifierKeys)
			{
				case Keys.Tab | Keys.Control:
				case Keys.Tab | Keys.Shift | Keys.Control:
				case Keys.Return | Keys.Shift | Keys.Alt:
					return true;
				default:
					return false;
			}
		}

		public static bool IsEditorMnemonicMsg(Message msg)
		{
			char ch = (char)(int)msg.WParam;
			return msg.Msg == 260 && Control.ModifierKeys == Keys.Alt && ((int)ch >= 33 && (int)ch < 97);
		}

		public static QueryItem GetQueryItemFromPath(string path, string projectName)
		{
			string[] strArray = path.Split(cPathSeperators);
			int length = strArray.Length;
			QueryItem queryItem = (QueryItem)TfsContext.WorkItemStore.Projects[projectName].QueryHierarchy;
			for (int index = 0; index < length; ++index)
			{
				string name = strArray[index];
				QueryFolder queryFolder = queryItem as QueryFolder;
				if (queryFolder != null && queryFolder.Contains(name))
				{
					queryItem = queryFolder[name];
				}
				else
				{
					queryItem = (QueryItem)null;
					break;
				}
			}
			return queryItem;
		}

		private static char[] cPathSeperators = new char[2]{'/','\\'};

		private static string DefaultOfficeTempFilePath
		{
			get
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			}
		}
	}
}