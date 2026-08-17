using System.IO;

namespace CheckoutAndBuild.Core.Git
{
	/// <summary>One entry of "git worktree list --porcelain".</summary>
	public sealed class GitWorktree
	{
		public string Path { get; set; }

		public string HeadSha { get; set; }

		public string Branch { get; set; } = "";

		public bool IsMain { get; set; }

		public bool IsDetached { get; set; }

		public bool IsLocked { get; set; }

		public string LockReason { get; set; }

		public bool IsPrunable { get; set; }

		public string Name => System.IO.Path.GetFileName(Path?.TrimEnd(System.IO.Path.DirectorySeparatorChar, '/'));

		public string ShortSha => string.IsNullOrEmpty(HeadSha) ? "" : HeadSha.Substring(0, System.Math.Min(8, HeadSha.Length));

		public bool Exists => !string.IsNullOrEmpty(Path) && Directory.Exists(Path);
	}
}
