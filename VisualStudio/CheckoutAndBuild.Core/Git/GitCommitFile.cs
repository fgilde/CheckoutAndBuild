namespace CheckoutAndBuild.Core.Git
{
    /// <summary>One file of a commit from "git show --name-status" (see <see cref="GitService.GetCommitFilesAsync"/>).</summary>
    public sealed class GitCommitFile
    {
        public GitCommitFile(string status, string filePath)
        {
            Status = status;
            FilePath = filePath;
        }

        public string Status { get; }
        public string FilePath { get; }
    }
}
