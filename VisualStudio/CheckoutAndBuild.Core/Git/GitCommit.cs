namespace CheckoutAndBuild.Core.Git
{
    /// <summary>One commit from "git log" (see <see cref="GitService.GetHistoryAsync"/>).</summary>
    public sealed class GitCommit
    {
        public string Sha { get; set; }
        public string ShortSha { get; set; }
        public string Author { get; set; }
        public string Date { get; set; }
        public string Message { get; set; }
    }
}
