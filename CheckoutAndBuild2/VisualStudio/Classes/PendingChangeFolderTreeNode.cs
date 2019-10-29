using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Classes
{
	public class PendingChangeFolderTreeNode : PendingChangeTreeNode
	{
		internal PendingChangeFolderTreeNode(PendingChange pendingChange, bool useServerPath)
			: base(pendingChange, useServerPath)
		{
		}

		internal PendingChangeFolderTreeNode(string segment, bool useServerPath)
			: base(segment, useServerPath)
		{
		}
	}
}