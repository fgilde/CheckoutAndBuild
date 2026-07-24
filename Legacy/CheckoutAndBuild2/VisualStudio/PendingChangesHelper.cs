using System;
using System.Globalization;
using System.IO;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using Microsoft.TeamFoundation.VersionControl.Common.Internal;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	internal static class PendingChangesHelper
	{
		private static readonly char[] serverPathSeparator = new char[1]
		{
			'/'
		};

		private static readonly char[] localPathSeparator = new char[1]
		{
			Path.DirectorySeparatorChar
		};

		internal static string FormatDisplayChangeType(PendingChange pendingChange, bool useServerPath)
		{
			var str = string.Empty;
			if (pendingChange != null)
			{
				var changeType = pendingChange.ChangeType;
				if ((changeType & ChangeType.None) != 0)
					str = string.Empty;
				else if (changeType == ChangeType.Edit)
					str = string.Empty;
				else if (changeType == ChangeType.Delete)
					str = string.Empty;
				else if (changeType == ChangeType.Merge)
					str = string.Format(CultureInfo.CurrentCulture, "[{0}]", Resources.Get("ChangeTypeMerge"));
				else if (changeType == ChangeType.Branch)
					str = string.Format(CultureInfo.CurrentCulture, "[{0}]", Resources.Get("ChangeTypeBranch"));
				else if (changeType == ChangeType.Undelete)
					str = string.Format(CultureInfo.CurrentCulture, "[{0}]", Resources.Get("ChangeTypeUndelete"));
				else if (changeType == ChangeType.Rollback)
					str = string.Format(CultureInfo.CurrentCulture, "[{0}]", Resources.Get("ChangeTypeRollback"));
				else if (changeType == ChangeType.Encoding)
					str = string.Format(CultureInfo.CurrentCulture, "[{0}]", Resources.Get("ChangeTypeFileType"));
				else if (pendingChange.IsRename)
				{
					var originalName = GetOriginalName(pendingChange, useServerPath);
					var flag = !string.IsNullOrEmpty(originalName);
					if (changeType == ChangeType.Rename && flag)
						str = string.Format(CultureInfo.CurrentCulture, "[{0}]", originalName);
					else if (flag)
						str = string.Format(CultureInfo.CurrentCulture, "[{0}][{1}]", originalName,
							PendingChange.GetLocalizedStringForChangeType(changeType, true));
					else
						str = string.Format(CultureInfo.CurrentCulture, "[{0}]",
							PendingChange.GetLocalizedStringForChangeType(changeType, true));
				}
				else
					str = string.Format(CultureInfo.CurrentCulture, "[{0}]",
						PendingChange.GetLocalizedStringForChangeType(changeType, true));
			}
			return str;
		}

		internal static string GetOriginalName(PendingChange pendingChange, bool useServerPath)
		{
            

            string x = null;
			var itemName = GetItemName(pendingChange, useServerPath);
			if (!string.IsNullOrEmpty(pendingChange.SourceLocalItem) && !string.IsNullOrEmpty(pendingChange.LocalItem))
			{
				x = FileSpec.GetFileName(pendingChange.SourceLocalItem);
				if (
					!FileSpec.Equals(FileSpec.GetDirectoryName(pendingChange.SourceLocalItem),
						FileSpec.GetDirectoryName(pendingChange.LocalItem)))
					x = pendingChange.SourceLocalItem;
				if (string.Equals(x, itemName, StringComparison.Ordinal))
					x = null;
			}
			if (x == null && !string.IsNullOrEmpty(pendingChange.SourceServerItem) &&
				!string.IsNullOrEmpty(pendingChange.ServerItem))
			{
				x = VersionControlPath.GetFileName(pendingChange.SourceServerItem);
				if (
					!VersionControlPath.Equals(VersionControlPath.GetFolderName(pendingChange.SourceServerItem),
						VersionControlPath.GetFolderName(pendingChange.ServerItem)))
					x = pendingChange.SourceServerItem;
				if (string.Equals(x, itemName, StringComparison.Ordinal))
					x = null;
			}
			return x;
		}

		internal static string GetItemPath(PendingChange pendingChange, bool useServerPath)
		{
			if (useServerPath || string.IsNullOrEmpty(pendingChange.LocalItem))
				return pendingChange.ServerItem;
			return pendingChange.LocalItem;
		}

		internal static string GetItemName(PendingChange pendingChange, bool useServerPath)
		{
			if (useServerPath || string.IsNullOrEmpty(pendingChange.LocalItem))
				return VersionControlPath.GetFileName(pendingChange.ServerItem);
			return FileSpec.GetFileName(pendingChange.LocalItem);
		}
	}
}