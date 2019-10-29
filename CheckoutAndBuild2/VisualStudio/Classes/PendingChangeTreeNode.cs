using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace FG.CheckoutAndBuild2.VisualStudio.Classes
{
	public class PendingChangeTreeNode : FileListViewItem
	{
		public PendingChange PendingChange
		{
			get { return (PendingChange) ItemInfo; }
			set { ItemInfo = value; }
		}

		private bool UseServerPath { get; set; }

		public override string ItemParentPath
		{
			get
			{
				if (!UseServerPath)
					return FileSpec.GetDirectoryName(ItemPath);
				return VersionControlPath.GetFolderName(ItemPath);
			}
		}

		public override string ItemParentRelativePath
		{
			get { return ItemParentPath; }
		}

		public string AutomationId
		{
			get { return string.Empty; }
		}

		public override string AutomationName
		{
			get
			{
				return ItemPath;
				//if (this.PendingChange != null)
				//	return PendingChangesResources.AutomationIdForPendingChangeListItem((object)this.ItemPath, (object)PendingChange.GetLocalizedStringForChangeType(this.PendingChange.ChangeType, true));
				//return PendingChangesResources.AutomationIdForFolderListItem((object)this.ItemPath);
			}
		}

		public string AutomationHelpText
		{
			get { return AutomationName; }
		}

		public PendingChangeTreeNode()
		{
		}

		internal PendingChangeTreeNode(PendingChange pendingChange, bool useServerPath)
		{
			PendingChange = pendingChange;
			UseServerPath = useServerPath;
			if (PendingChange == null)
				return;
			ItemText = PendingChangesHelper.GetItemName(PendingChange, UseServerPath);
			ItemPath = PendingChangesHelper.GetItemPath(PendingChange, UseServerPath);
			ItemSubText = PendingChangesHelper.FormatDisplayChangeType(PendingChange, UseServerPath);
			IsDeleted = pendingChange.IsDelete;
		}

		internal PendingChangeTreeNode(string segment, bool useServerPath)
		{
			PendingChange = null;
			ItemText = segment;
			UseServerPath = useServerPath;
		}

		public static List<PendingChangeTreeNode> FindEquivalentNodes(IList<PendingChangeTreeNode> candidateNodes,
			List<PendingChangeTreeNode> queryNodes)
		{
			var hash = new Dictionary<string, PendingChangeTreeNode>(StringComparer.CurrentCultureIgnoreCase);
			foreach (var pendingChangeTreeNode in queryNodes)
				hash[pendingChangeTreeNode.ItemPath] = pendingChangeTreeNode;
			return candidateNodes.Where(candidateNode => hash.ContainsKey(candidateNode.ItemPath)).ToList();
		}

		protected override string GetToolTipText()
		{
			if (PendingChange == null)
				return ItemPath;
			return PendingChange.ToolTipText;
		}

		protected override char GetSegmentSeparator()
		{
			return PendingChangesFileListViewHelper.GetSegmentSeparator(UseServerPath);
		}
	}
}