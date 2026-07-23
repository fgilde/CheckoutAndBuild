using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Git;

namespace CheckoutAndBuild.Core.Tests;

public sealed class GitServiceTests : IDisposable
{
    private readonly string repoDir;
    private readonly GitService git = new();

    public GitServiceTests()
    {
        repoDir = Path.Combine(Path.GetTempPath(), "coab-git-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoDir);
        Run("init -b master");
        Run("config user.email test@example.com");
        Run("config user.name TestUser");
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "hello");
        Run("add .");
        Run("commit -m initial");
    }

    private void Run(string args)
    {
        var result = ProcessRunner.RunAsync("git", $"-C \"{repoDir}\" {args}").GetAwaiter().GetResult();
        Assert.True(result.Success, $"git {args} failed: {result.StdErr}");
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(repoDir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(repoDir, true);
        }
        catch
        {
            // best effort — temp dir cleanup
        }
    }

    [Fact]
    public void IsGitRepository_InsideRepo_True()
    {
        Assert.True(git.IsGitRepository(repoDir));
    }

    [Fact]
    public void IsGitRepository_PlainDirectory_False()
    {
        var plainDir = Path.Combine(Path.GetTempPath(), "coab-nonrepo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(plainDir);
        try
        {
            Assert.False(git.IsGitRepository(plainDir));
        }
        finally
        {
            Directory.Delete(plainDir, true);
        }
    }

    [Fact]
    public void GetRepositoryRoot_ReturnsRepoDir()
    {
        var root = git.GetRepositoryRoot(repoDir);
        Assert.Equal(Path.GetFullPath(repoDir).TrimEnd('\\'), Path.GetFullPath(root).TrimEnd('\\'), ignoreCase: true);
    }

    [Fact]
    public async Task GetCurrentBranch_AfterCheckoutNewBranch_ReturnsBranchName()
    {
        Run("checkout -b feature/x");
        Assert.Equal("feature/x", await git.GetCurrentBranchAsync(repoDir));

        await git.CheckoutBranchAsync(repoDir, "master");
        Assert.Equal("master", await git.GetCurrentBranchAsync(repoDir));
    }

    [Fact]
    public async Task GetBranches_ContainsBothBranches()
    {
        Run("checkout -b feature/x");
        var branches = await git.GetBranchesAsync(repoDir);
        Assert.Contains("master", branches);
        Assert.Contains("feature/x", branches);
    }

    [Fact]
    public async Task StashRoundtrip_PushListDrop()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");

        await git.StashPushAsync(repoDir, "my test stash");

        var stashes = await git.GetStashesAsync(repoDir);
        var stash = Assert.Single(stashes);
        Assert.Equal("stash@{0}", stash.Id);
        Assert.Equal("master", stash.Branch);
        Assert.Contains("my test stash", stash.Name);
        Assert.False(string.IsNullOrEmpty(stash.Hash));
        Assert.False(string.IsNullOrEmpty(stash.TimeInfo));
        Assert.Equal("TestUser", stash.Creator);
        Assert.Equal(repoDir, stash.GitDirectory);

        await git.StashDropAsync(repoDir, 0);
        Assert.Empty(await git.GetStashesAsync(repoDir));
    }

    [Fact]
    public async Task CheckoutBranch_Nonexistent_ThrowsWithStdErr()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => git.CheckoutBranchAsync(repoDir, "does-not-exist"));
        Assert.Contains("does-not-exist", ex.Message);
    }
}
