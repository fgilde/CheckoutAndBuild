using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Git;

namespace CheckoutAndBuild.Core.Tests;

public sealed class WorktreeTests : IDisposable
{
	private readonly string rootDir;
	private readonly string repoDir;
	private readonly GitService git = new();

	public WorktreeTests()
	{
		rootDir = Path.Combine(Path.GetTempPath(), "coab-wt-tests-" + Guid.NewGuid().ToString("N"));
		repoDir = Path.Combine(rootDir, "Repo");
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
			foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
				File.SetAttributes(file, FileAttributes.Normal);
			Directory.Delete(rootDir, true);
		}
		catch
		{
		}
	}

	[Fact]
	public async Task Worktrees_AddListRemovePrune_Roundtrip()
	{
		string wtPath = GitService.GetDefaultWorktreePath(repoDir, "feature/one");
		Assert.Equal(Path.Combine(rootDir, "Repo-feature-one"), wtPath);

		await git.AddWorktreeAsync(repoDir, wtPath, "feature/one", createBranch: true);

		var worktrees = await git.GetWorktreesAsync(repoDir);
		Assert.Equal(2, worktrees.Count);
		Assert.True(worktrees[0].IsMain);
		Assert.Equal("master", worktrees[0].Branch);
		var linked = worktrees[1];
		Assert.False(linked.IsMain);
		Assert.Equal("feature/one", linked.Branch);
		Assert.False(linked.IsDetached);
		Assert.True(linked.Exists);
		Assert.NotEmpty(linked.ShortSha);

		await git.RemoveWorktreeAsync(repoDir, wtPath);
		worktrees = await git.GetWorktreesAsync(repoDir);
		Assert.Single(worktrees);
	}

	[Fact]
	public async Task Worktrees_PrunableAfterManualDelete_AndPrune()
	{
		string wtPath = Path.Combine(rootDir, "Repo-stale");
		await git.AddWorktreeAsync(repoDir, wtPath, "stale", createBranch: true);
		foreach (var file in Directory.EnumerateFiles(wtPath, "*", SearchOption.AllDirectories))
			File.SetAttributes(file, FileAttributes.Normal);
		Directory.Delete(wtPath, true);

		var worktrees = await git.GetWorktreesAsync(repoDir);
		Assert.Contains(worktrees, w => !w.IsMain && w.IsPrunable);

		await git.PruneWorktreesAsync(repoDir);
		worktrees = await git.GetWorktreesAsync(repoDir);
		Assert.Single(worktrees);
	}

	[Fact]
	public async Task Worktrees_ExistingBranch_CheckedOutInWorktree()
	{
		Run("branch existing");
		string wtPath = Path.Combine(rootDir, "Repo-existing");
		await git.AddWorktreeAsync(repoDir, wtPath, "existing", createBranch: false);

		var worktrees = await git.GetWorktreesAsync(repoDir);
		Assert.Contains(worktrees, w => w.Branch == "existing" && !w.IsMain);
	}
}
