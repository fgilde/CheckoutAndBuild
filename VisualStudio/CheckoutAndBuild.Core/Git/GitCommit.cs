namespace CheckoutAndBuild.Core.Git
{
    /// <summary>One commit from "git log" (see <see cref="GitService.GetHistoryAsync"/>).</summary>
    public sealed class GitCommit
    {
        public string Sha { get; set; }
        public string ShortSha { get; set; }
        public string Author { get; set; }
        /// <summary>Commit date as reported by git (%ci, ISO-like with timezone).</summary>
        public string Date { get; set; }
        public string Message { get; set; }
    }
}
