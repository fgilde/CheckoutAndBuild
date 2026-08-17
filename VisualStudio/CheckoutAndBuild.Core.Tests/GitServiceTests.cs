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

    [Fact]
    public async Task GetStatus_ParsesStagedUnstagedAndUntracked()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");     // unstaged modified
        File.WriteAllText(Path.Combine(repoDir, "staged.txt"), "new");
        Run("add staged.txt");                                                // staged added
        File.WriteAllText(Path.Combine(repoDir, "untracked.txt"), "loose");   // untracked

        var changes = await git.GetStatusAsync(repoDir);

        var modified = changes.Single(c => c.FilePath == "README.md");
        Assert.Equal(GitChangeType.Modified, modified.ChangeType);
        Assert.False(modified.IsStaged);

        var added = changes.Single(c => c.FilePath == "staged.txt");
        Assert.Equal(GitChangeType.Added, added.ChangeType);
        Assert.True(added.IsStaged);

        var untracked = changes.Single(c => c.FilePath == "untracked.txt");
        Assert.Equal(GitChangeType.Untracked, untracked.ChangeType);
        Assert.False(untracked.IsStaged);
    }

    [Fact]
    public async Task GetStatus_StagedRename_ReportsNewPath()
    {
        Run("mv README.md RENAMED.md");

        var changes = await git.GetStatusAsync(repoDir);

        var renamed = Assert.Single(changes);
        Assert.Equal(GitChangeType.Renamed, renamed.ChangeType);
        Assert.Equal("RENAMED.md", renamed.FilePath);
        Assert.True(renamed.IsStaged);
    }

    [Fact]
    public async Task GetStatus_CleanRepo_Empty()
    {
        Assert.Empty(await git.GetStatusAsync(repoDir));
    }

    [Fact]
    public async Task GetDiff_UnstagedAndPerFile_ContainsPatch()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");

        var diff = await git.GetDiffAsync(repoDir);
        Assert.Contains("README.md", diff);
        Assert.Contains("+changed", diff);

        var fileDiff = await git.GetDiffAsync(repoDir, "README.md");
        Assert.Contains("+changed", fileDiff);

        var stagedDiff = await git.GetDiffAsync(repoDir, staged: true);
        Assert.True(string.IsNullOrWhiteSpace(stagedDiff));
    }

    [Fact]
    public async Task ExportChangesAsPatch_WritesTrackedDiff()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        var target = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".patch");
        try
        {
            await git.ExportChangesAsPatchAsync(repoDir, target);
            var patch = File.ReadAllText(target);
            Assert.Contains("README.md", patch);
            Assert.Contains("+changed", patch);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public async Task ExportChangesAsZip_ContainsModifiedAndUntrackedWithFolders()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        Directory.CreateDirectory(Path.Combine(repoDir, "sub"));
        File.WriteAllText(Path.Combine(repoDir, "sub", "new.txt"), "x");
        var target = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await git.ExportChangesAsZipAsync(repoDir, target);
            using var zip = System.IO.Compression.ZipFile.OpenRead(target);
            Assert.Contains(zip.Entries, e => e.FullName == "README.md");
            Assert.Contains(zip.Entries, e => e.FullName == "sub/new.txt");
            Assert.Equal(2, zip.Entries.Count);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public async Task GetHistory_ReturnsCommitsNewestFirst()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        Run("commit -am second");

        var commits = await git.GetHistoryAsync(repoDir);

        Assert.Equal(2, commits.Count);
        Assert.Equal("second", commits[0].Message);
        Assert.Equal("initial", commits[1].Message);
        foreach (var commit in commits)
        {
            Assert.Equal(40, commit.Sha.Length);
            Assert.StartsWith(commit.ShortSha, commit.Sha);
            Assert.Equal("TestUser", commit.Author);
            Assert.False(string.IsNullOrEmpty(commit.Date));
        }
    }

    [Fact]
    public async Task GetHistory_RespectsMaxCount()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        Run("commit -am second");

        var commits = await git.GetHistoryAsync(repoDir, maxCount: 1);

        var commit = Assert.Single(commits);
        Assert.Equal("second", commit.Message);
    }

    [Fact]
    public async Task GetCommitDetails_ContainsMessageAndStat()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        Run("commit -am second");
        var commits = await git.GetHistoryAsync(repoDir);

        var details = await git.GetCommitDetailsAsync(repoDir, commits[0].Sha);

        Assert.Contains(commits[0].ShortSha, details);
        Assert.Contains("second", details);
        Assert.Contains("README.md", details);
    }

    [Fact]
    public async Task CreateBranch_WithAndWithoutCheckout()
    {
        await git.CreateBranchAsync(repoDir, "feature/no-switch", checkout: false);
        Assert.Equal("master", await git.GetCurrentBranchAsync(repoDir));

        await git.CreateBranchAsync(repoDir, "feature/switch");
        Assert.Equal("feature/switch", await git.GetCurrentBranchAsync(repoDir));

        var branches = await git.GetBranchesAsync(repoDir);
        Assert.Contains("feature/no-switch", branches);
        Assert.Contains("feature/switch", branches);
    }

    [Fact]
    public async Task DeleteBranch_UnmergedNeedsForce()
    {
        await git.CreateBranchAsync(repoDir, "merged", checkout: false);
        await git.DeleteBranchAsync(repoDir, "merged");
        Assert.DoesNotContain("merged", await git.GetBranchesAsync(repoDir));

        await git.CreateBranchAsync(repoDir, "unmerged");
        File.WriteAllText(Path.Combine(repoDir, "extra.txt"), "x");
        Run("add .");
        Run("commit -m extra");
        await git.CheckoutBranchAsync(repoDir, "master");

        await Assert.ThrowsAsync<InvalidOperationException>(() => git.DeleteBranchAsync(repoDir, "unmerged"));
        await git.DeleteBranchAsync(repoDir, "unmerged", force: true);
        Assert.DoesNotContain("unmerged", await git.GetBranchesAsync(repoDir));
    }

    [Fact]
    public async Task PushAndAheadBehind_AgainstLocalBareRemote()
    {
        var status = await git.GetAheadBehindAsync(repoDir);
        Assert.False(status.HasUpstream);
        Assert.Equal("master", status.Branch);

        var bareDir = Path.Combine(Path.GetTempPath(), "coab-git-bare-" + Guid.NewGuid().ToString("N"));
        try
        {
            var init = await ProcessRunner.RunAsync("git", $"init --bare \"{bareDir}\"");
            Assert.True(init.Success, init.StdErr);
            Run($"remote add origin \"{bareDir}\"");

            await git.PushAsync(repoDir, setUpstream: true);
            status = await git.GetAheadBehindAsync(repoDir);
            Assert.True(status.HasUpstream);
            Assert.Equal(0, status.Ahead);
            Assert.Equal(0, status.Behind);

            File.WriteAllText(Path.Combine(repoDir, "README.md"), "ahead");
            Run("commit -am ahead");
            status = await git.GetAheadBehindAsync(repoDir, "master");
            Assert.Equal(1, status.Ahead);
            Assert.Equal(0, status.Behind);

            await git.PushAsync(repoDir);
            Run("reset --hard HEAD~1");
            status = await git.GetAheadBehindAsync(repoDir);
            Assert.Equal(0, status.Ahead);
            Assert.Equal(1, status.Behind);
        }
        finally
        {
            try { Directory.Delete(bareDir, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task GetHistory_FiltersByAuthorAndGrep()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        Run("-c user.name=OtherUser -c user.email=other@example.com commit -am \"fix: something else\"");

        var byAuthor = await git.GetHistoryAsync(repoDir, author: "OtherUser");
        var commit = Assert.Single(byAuthor);
        Assert.Equal("OtherUser", commit.Author);

        var byGrep = await git.GetHistoryAsync(repoDir, grep: "INITIAL");
        commit = Assert.Single(byGrep);
        Assert.Equal("initial", commit.Message);

        var recent = await git.GetHistoryAsync(repoDir, sinceDays: 1);
        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public async Task GetCommitFiles_ReturnsStatusAndPath()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        File.WriteAllText(Path.Combine(repoDir, "new.txt"), "x");
        Run("add .");
        Run("commit -m second");
        var commits = await git.GetHistoryAsync(repoDir);

        var files = await git.GetCommitFilesAsync(repoDir, commits[0].Sha);

        Assert.Equal(2, files.Count);
        var modified = files.Single(f => f.FilePath == "README.md");
        Assert.Equal("M", modified.Status);
        var added = files.Single(f => f.FilePath == "new.txt");
        Assert.Equal("A", added.Status);
    }

    [Fact]
    public async Task GetFileDiff_ContainsPatchForSingleFile()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        File.WriteAllText(Path.Combine(repoDir, "new.txt"), "x");
        Run("add .");
        Run("commit -m second");
        var commits = await git.GetHistoryAsync(repoDir);

        var diff = await git.GetFileDiffAsync(repoDir, commits[0].Sha, "README.md");

        Assert.Contains("+changed", diff);
        Assert.DoesNotContain("new.txt", diff);
    }

    [Fact]
    public async Task GetConfiguredUser_ReturnsUserName()
    {
        Assert.Equal("TestUser", await git.GetConfiguredUserAsync(repoDir));
    }

    [Fact]
    public async Task GetStashDiff_ReturnsPatch()
    {
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "changed");
        await git.StashPushAsync(repoDir, "diff test");

        var diff = await git.GetStashDiffAsync(repoDir, 0);

        Assert.Contains("README.md", diff);
        Assert.Contains("+changed", diff);
    }
}
