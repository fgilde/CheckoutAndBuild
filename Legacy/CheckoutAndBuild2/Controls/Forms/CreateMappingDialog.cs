using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public partial class CreateMappingDialog : Form
	{
		private readonly string defaultDerverDir;
		private readonly string defaultLocalDir;

		public string LocalItem
		{
			get { return localItemPath.Text; }
			set { localItemPath.Text = value; }
		}

		public string ServerItem
		{
			get { return serverItemPath.Text; }
			set { serverItemPath.Text = value; }
		}

		public static bool CreateNewMapping(Workspace workspace, TfsContext tfs)
		{
			if (workspace == null)
				return false;
			//var p = listViewDetails.Items.ToEnumerable().Select(item => item.SubItems[1]).OrderBy(item => item.Name);
			string defaultServerDir = workspace.Folders.FindMatchingServerBasePath();
			string defaultLocalDir = workspace.Folders.FindMatchingLocalBasePath();

			var dlg = new CreateMappingDialog(tfs, workspace, defaultServerDir, defaultLocalDir);

			if (!String.IsNullOrEmpty(defaultLocalDir))
				dlg.LocalItem = Path.GetDirectoryName(defaultLocalDir);
			if (!String.IsNullOrEmpty(defaultServerDir))
				dlg.ServerItem = defaultServerDir;
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				string serverItem = dlg.ServerItem;
				string localItem = dlg.LocalItem;
				if (serverItem.StartsWith("$"))
				{
					try
					{
						workspace.Map(serverItem, localItem);
						return true;
					}
					catch (Exception)
					{
						return false;
					}
				}
				return false;
			}
			return false;
		}

		public static bool ChangeMapping(Workspace workspace, TfsContext tfs, WorkingFolder folder)
		{
			if (workspace == null)
				return false;

			var dlg = new CreateMappingDialog(tfs, workspace, folder.ServerItem, folder.LocalItem)
			{
				Text = "Change Mapping",
				ServerItem = folder.ServerItem,
				LocalItem = folder.LocalItem
			};
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				string serverItem = dlg.ServerItem;
				string localItem = dlg.LocalItem;
				if (serverItem.StartsWith("$"))
				{
					try
					{
						workspace.DeleteMapping(folder);
						workspace.Map(serverItem, localItem);
						return true;
					}
					catch (Exception)
					{
						return false;
					}
				}
				return false;
			}
			return false;
		}


		public CreateMappingDialog(TfsContext tfsContext, Workspace workspace, string defaultDerverDir, string defaultLocalDir)
		{
			try
			{
				InitializeComponent();
				localItemPath.Workspace = workspace;
				serverItemPath.Workspace = workspace;
				localItemPath.TfsHelper = tfsContext;
				serverItemPath.TfsHelper = tfsContext;
				serverItemPath.Text = @"$/";
				localItemPath.Text = @"C:\";

				string restServer = defaultDerverDir.Split('/').LastOrDefault();
				this.defaultDerverDir = defaultDerverDir;
				this.defaultLocalDir = Path.GetDirectoryName(defaultLocalDir);
				if (!string.IsNullOrWhiteSpace(restServer))
					this.defaultDerverDir = this.defaultDerverDir.Replace(restServer, "");


				if (!string.IsNullOrWhiteSpace(this.defaultDerverDir) && this.defaultDerverDir.StartsWith("$"))
					serverItemPath.Text = this.defaultDerverDir;

				if (!string.IsNullOrWhiteSpace(this.defaultLocalDir))
					localItemPath.Text = this.defaultLocalDir;
			}
			catch (Exception)
			{
				serverItemPath.Text = @"$/";
				localItemPath.Text = @"C:\";
			}

			serverItemPath.WorkingFolder = new WorkingFolder(ServerItem, LocalItem);
			localItemPath.WorkingFolder = new WorkingFolder(ServerItem, LocalItem);
			serverItemPath.FolderBrowsed += ServerItemPathOnFolderBrowsed;
		}

		private void ServerItemPathOnFolderBrowsed(object sender, EventArgs<string> e)
		{
			try
			{
				string suggestionLocal = e.Value.Replace(defaultDerverDir, string.Empty).Replace('/', '\\');
				localItemPath.Text = Path.Combine(defaultLocalDir, suggestionLocal);
			}
			catch
			{}
		}
	}
}
