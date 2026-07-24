namespace CheckoutAndBuild.Core.Git
{
    /// <summary>Ahead/behind counts of a branch against its upstream (see <see cref="GitService.GetAheadBehindAsync"/>).</summary>
    public sealed class BranchSyncStatus
    {
        public string Branch { get; set; }
        public int Ahead { get; set; }
        public int Behind { get; set; }
        public bool HasUpstream { get; set; }
    }
}
