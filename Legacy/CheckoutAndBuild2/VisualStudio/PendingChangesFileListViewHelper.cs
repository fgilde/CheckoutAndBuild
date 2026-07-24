using System;
using System.IO;
using System.Text;
using FG.CheckoutAndBuild2.VisualStudio.Classes;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	public class PendingChangesFileListViewHelper : FileListViewHelper<PendingChange, PendingChangeTreeNode>
	{
		private static readonly char[] c_serverPathSeparator = new char[1]
		{
			'/'
		};

		private static readonly char[] c_localPathSeparator = new char[1]
		{
			Path.DirectorySeparatorChar
		};

		private bool UseServerPath { get; set; }

		public PendingChangesFileListViewHelper(bool useServerPath)
		{
			UseServerPath = useServerPath;
		}

		public override string GetName(PendingChange t)
		{
			return PendingChangesHelper.GetItemName(t, UseServerPath);
		}

		public override string GetFolderName(PendingChange t)
		{
			return PendingChangesHelper.GetItemPath(t, UseServerPath);
		}

		public override string[] SplitPath(PendingChange t)
		{
			return UseServerPath || string.IsNullOrEmpty(t.LocalItem)
				? t.ServerItem.Split(c_serverPathSeparator, StringSplitOptions.RemoveEmptyEntries)
				: FileSpec.SplitPath(t.LocalItem);
		}

		public override void BuildPath(StringBuilder path, string segment)
		{
			if (path.Length > 0)
				path.Append(GetSegmentSeparator(UseServerPath));
			path.Append(segment);
		}

		public override bool IsFolder(PendingChange t)
		{
			return t.ItemType == ItemType.Folder;
		}

        public override PendingChangeTreeNode CreateItem(string itemText, PendingChange t, object userData)
        {
            return new PendingChangeTreeNode(t, UseServerPath);
        }

        public override PendingChangeTreeNode CreateFolderItem(string itemText, PendingChange t, object userData)
        {
            var changeFolderTreeNode = t != null
                ? new PendingChangeFolderTreeNode(t, UseServerPath)
                : new PendingChangeFolderTreeNode(itemText, UseServerPath);
            changeFolderTreeNode.IsFolder = true;
            changeFolderTreeNode.IsExpanded = true;
            return changeFolderTreeNode;
        }


        public override bool FilterItem(PendingChange t, string filter, bool isWildcard)
		{
			return true;
		}

		public static char GetSegmentSeparator(bool useServerPath)
		{
			if (!useServerPath)
				return c_localPathSeparator[0];
			return c_serverPathSeparator[0];
		}
	}
}