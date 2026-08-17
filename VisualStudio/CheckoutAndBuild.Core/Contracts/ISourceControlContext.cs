using System.ComponentModel.Composition;

namespace CheckoutAndBuild.Core.Contracts
{
    /// <summary>
    /// Git-only source control context (replaces the old TFS-bound ITfsContext).
    /// </summary>
    [InheritedExport]
    public interface ISourceControlContext
    {
        string RepositoryPath { get; }

        string CurrentBranch { get; }

        bool IsGitRepository { get; }
    }
}
