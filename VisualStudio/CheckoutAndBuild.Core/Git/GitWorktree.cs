using System.IO;

namespace CheckoutAndBuild.Core.Git
{
	/// <summary>One entry of "git worktree list --porcelain".</summary>
	public sealed class GitWorktree
	{
		/// <summary>Absolute path of the working tree.</summary>
		public string Path { get; set; }

		public string HeadSha { get; set; }

		/// <summary>Short branch name; empty when detached.</summary>
		public string Branch { get; set; } = "";

		/// <summary>First entry of the list — the main working tree (cannot be removed).</summary>
		public bool IsMain { get; set; }

		public bool IsDetached { get; set; }

		public bool IsLocked { get; set; }

		/// <summary>Reason from "locked [reason]" when present.</summary>
		public string LockReason { get; set; }

		/// <summary>Directory is gone/stale — "git worktree prune" would drop it.</summary>
		public bool IsPrunable { get; set; }

		public string Name => System.IO.Path.GetFileName(Path?.TrimEnd(System.IO.Path.DirectorySeparatorChar, '/'));

		public string ShortSha => string.IsNullOrEmpty(HeadSha) ? "" : HeadSha.Substring(0, System.Math.Min(8, HeadSha.Length));

		public bool Exists => !string.IsNullOrEmpty(Path) && Directory.Exists(Path);
	}
}
