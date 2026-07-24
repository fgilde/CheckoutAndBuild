namespace CheckoutAndBuild.Core.Git
{
    public enum GitChangeType
    {
        Added,
        Modified,
        Deleted,
        Renamed,
        Untracked,
        Conflicted
    }

    /// <summary>One entry of "git status --porcelain" (repo-relative path, forward slashes).</summary>
    public sealed class GitChange
    {
        public GitChange(string filePath, GitChangeType changeType, bool isStaged)
        {
            FilePath = filePath;
            ChangeType = changeType;
            IsStaged = isStaged;
        }

        public string FilePath { get; }
        public GitChangeType ChangeType { get; }
        public bool IsStaged { get; }
    }
}
