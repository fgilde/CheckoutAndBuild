using System.ComponentModel.Composition;

namespace CheckoutAndBuild.Core.Contracts
{
    /// <summary>
    /// Git-only source control context (replaces the old TFS-bound ITfsContext).
    /// </summary>
    [InheritedExport]
    public interface ISourceControlContext
    {
        /// <summary>Root directory of the current git repository.</summary>
        string RepositoryPath { get; }

        /// <summary>Name of the currently checked out branch.</summary>
        string CurrentBranch { get; }

        /// <summary>True if <see cref="RepositoryPath"/> points into a git repository.</summary>
        bool IsGitRepository { get; }
    }
}
