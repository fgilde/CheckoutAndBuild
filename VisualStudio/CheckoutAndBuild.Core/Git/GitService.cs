using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Execution;

namespace CheckoutAndBuild.Core.Git
{
    /// <summary>
    /// Git operations executed out-of-process via git.exe (see CheckoutAndBuild2/Git/GitHelper.cs for legacy semantics).
    /// </summary>
    public sealed class GitService
    {
        private const string StashListFormat = "%h%x09%gd%x09%ci%x09%an%x09%gs";

        public bool IsGitRepository(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;
            var result = ProcessRunner.RunAsync("git", GitArgs(directory, "rev-parse --is-inside-work-tree"))
                .GetAwaiter().GetResult();
            return result.Success && result.StdOut.Trim() == "true";
        }

        public string GetRepositoryRoot(string directory)
        {
            var result = RunGitAsync(directory, "rev-parse --show-toplevel").GetAwaiter().GetResult();
            return result.StdOut.Trim().Replace('/', Path.DirectorySeparatorChar);
        }

        public async Task<string> GetCurrentBranchAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, "rev-parse --abbrev-ref HEAD", ct: ct).ConfigureAwait(false);
            return result.StdOut.Trim();
        }

        public async Task<IReadOnlyList<string>> GetBranchesAsync(string repoDir, bool includeRemote = false, CancellationToken ct = default)
        {
            var args = (includeRemote ? "branch -a" : "branch") + " --format=%(refname:short)";
            var result = await RunGitAsync(repoDir, args, ct: ct).ConfigureAwait(false);
            return SplitLines(result.StdOut).ToList();
        }

        public Task PullAsync(string repoDir, Action<string> onOutput = null, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, "pull", onOutput, ct);
        }

        public Task FetchAsync(string repoDir, Action<string> onOutput = null, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, "fetch", onOutput, ct);
        }

        /// <summary>Force push with lease (refuses when the remote moved since the last fetch).</summary>
        public Task ForcePushAsync(string repoDir, Action<string> onOutput = null, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, "push --force-with-lease", onOutput, ct);
        }

        /// <summary>Applies a patch file to the working tree (git apply --3way falls back to conflict markers).</summary>
        public Task ApplyPatchAsync(string repoDir, string patchFile, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"apply --3way \"{patchFile}\"", ct: ct);
        }

        /// <summary>Remote URL of origin, or null.</summary>
        public async Task<string> GetRemoteUrlAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, "config --get remote.origin.url"),
                cancellationToken: ct).ConfigureAwait(false);
            string url = result.StdOut.Trim();
            return result.Success && url.Length > 0 ? url : null;
        }

        /// <summary>Default branch name (origin/HEAD), falling back to main/master detection.</summary>
        public async Task<string> GetDefaultBranchAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, "symbolic-ref refs/remotes/origin/HEAD --short"),
                cancellationToken: ct).ConfigureAwait(false);
            if (result.Success)
            {
                string name = result.StdOut.Trim();
                int slash = name.IndexOf('/');
                if (slash >= 0)
                    return name.Substring(slash + 1);
            }
            var branches = await GetBranchesAsync(repoDir, ct: ct).ConfigureAwait(false);
            return branches.FirstOrDefault(b => b == "main") ?? branches.FirstOrDefault(b => b == "master") ?? branches.FirstOrDefault();
        }

        /// <summary>Local branches fully merged into the target branch, excluding the target and the current branch.</summary>
        public async Task<IReadOnlyList<string>> GetMergedBranchesAsync(string repoDir, string targetBranch, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, $"branch --merged \"{targetBranch}\" --format=%(refname:short)", ct: ct).ConfigureAwait(false);
            string current = await GetCurrentBranchAsync(repoDir, ct).ConfigureAwait(false);
            return SplitLines(result.StdOut)
                .Where(b => b != targetBranch && b != current)
                .ToList();
        }

        /// <summary>True when the branch exists locally or on origin.</summary>
        public async Task<bool> BranchExistsAsync(string repoDir, string branch, CancellationToken ct = default)
        {
            var local = await ProcessRunner.RunAsync("git", GitArgs(repoDir, $"rev-parse --verify --quiet \"refs/heads/{branch}\""),
                cancellationToken: ct).ConfigureAwait(false);
            if (local.Success)
                return true;
            var remote = await ProcessRunner.RunAsync("git", GitArgs(repoDir, $"rev-parse --verify --quiet \"refs/remotes/origin/{branch}\""),
                cancellationToken: ct).ConfigureAwait(false);
            return remote.Success;
        }

        #region worktrees

        /// <summary>All working trees of the repository (git worktree list --porcelain; first entry is the main tree).</summary>
        public async Task<IReadOnlyList<GitWorktree>> GetWorktreesAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, "worktree list --porcelain", ct: ct).ConfigureAwait(false);
            var worktrees = new List<GitWorktree>();
            GitWorktree current = null;
            foreach (string rawLine in result.StdOut.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    current = new GitWorktree
                    {
                        Path = line.Substring(9).Replace('/', Path.DirectorySeparatorChar),
                        IsMain = worktrees.Count == 0
                    };
                    worktrees.Add(current);
                }
                else if (current == null)
                {
                    continue;
                }
                else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                    current.HeadSha = line.Substring(5).Trim();
                else if (line.StartsWith("branch ", StringComparison.Ordinal))
                    current.Branch = line.Substring(7).Trim().Replace("refs/heads/", "");
                else if (line == "detached")
                    current.IsDetached = true;
                else if (line.StartsWith("locked", StringComparison.Ordinal))
                {
                    current.IsLocked = true;
                    current.LockReason = line.Length > 7 ? line.Substring(7).Trim() : null;
                }
                else if (line.StartsWith("prunable", StringComparison.Ordinal))
                    current.IsPrunable = true;
            }
            return worktrees;
        }

        /// <summary>Adds a worktree at <paramref name="path"/> for the branch (created when <paramref name="createBranch"/>).</summary>
        public Task AddWorktreeAsync(string repoDir, string path, string branch, bool createBranch, CancellationToken ct = default)
        {
            return createBranch
                ? RunGitAsync(repoDir, $"worktree add -b \"{branch}\" \"{path}\"", ct: ct)
                : RunGitAsync(repoDir, $"worktree add \"{path}\" \"{branch}\"", ct: ct);
        }

        /// <summary>Removes a worktree (force also with dirty working tree).</summary>
        public Task RemoveWorktreeAsync(string repoDir, string worktreePath, bool force = false, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"worktree remove {(force ? "--force " : "")}\"{worktreePath}\"", ct: ct);
        }

        /// <summary>Drops stale worktree bookkeeping for deleted directories.</summary>
        public Task PruneWorktreesAsync(string repoDir, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, "worktree prune", ct: ct);
        }

        /// <summary>Merges origin/&lt;default branch&gt; into the worktree's current branch (fetch first).</summary>
        public async Task UpdateFromBaseAsync(string worktreePath, Action<string> onOutput = null, CancellationToken ct = default)
        {
            await FetchAsync(worktreePath, onOutput, ct).ConfigureAwait(false);
            string baseBranch = await GetDefaultBranchAsync(worktreePath, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(baseBranch))
                throw new InvalidOperationException("No default branch found.");
            await RunGitAsync(worktreePath, $"merge \"origin/{baseBranch}\" --no-edit", onOutput, ct).ConfigureAwait(false);
        }

        /// <summary>Sibling folders whose .git file points to worktree metadata that no longer exists (broken leftovers).</summary>
        public static IReadOnlyList<string> FindOrphanWorktreeDirectories(string repoDir)
        {
            var orphans = new List<string>();
            string root = repoDir.TrimEnd(Path.DirectorySeparatorChar, '/');
            string parent = Path.GetDirectoryName(root);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                return orphans;
            foreach (string candidate in Directory.GetDirectories(parent))
            {
                if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
                    continue;
                string gitFile = Path.Combine(candidate, ".git");
                if (!File.Exists(gitFile))
                    continue;
                string content;
                try { content = File.ReadAllText(gitFile); }
                catch { continue; }
                if (!content.StartsWith("gitdir:", StringComparison.Ordinal))
                    continue;
                string gitDir = content.Substring(7).Trim().Replace('/', Path.DirectorySeparatorChar);
                if (!Directory.Exists(gitDir))
                    orphans.Add(candidate);
            }
            return orphans;
        }

        /// <summary>Sibling-folder convention for new worktrees: &lt;parent&gt;/&lt;repoName&gt;-&lt;branch&gt; (slashes become dashes).</summary>
        public static string GetDefaultWorktreePath(string repoDir, string branch)
        {
            string root = repoDir.TrimEnd(Path.DirectorySeparatorChar, '/');
            string name = Path.GetFileName(root);
            string parent = Path.GetDirectoryName(root) ?? root;
            string sanitized = (branch ?? "").Replace('/', '-').Replace('\\', '-');
            return Path.Combine(parent, $"{name}-{sanitized}");
        }

        #endregion

        /// <summary>Stages one file (git add).</summary>
        public Task StageAsync(string repoDir, string filePath, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"add -- \"{filePath}\"", ct: ct);
        }

        /// <summary>Unstages one file (git reset HEAD).</summary>
        public Task UnstageAsync(string repoDir, string filePath, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"reset HEAD -- \"{filePath}\"", ct: ct);
        }

        /// <summary>Discards the working tree changes of one file; untracked files are deleted.</summary>
        public async Task DiscardAsync(string repoDir, string filePath, bool isUntracked, CancellationToken ct = default)
        {
            if (isUntracked)
            {
                string fullPath = Path.Combine(repoDir, filePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return;
            }
            await RunGitAsync(repoDir, $"checkout HEAD -- \"{filePath}\"", ct: ct).ConfigureAwait(false);
        }

        /// <summary>Commits everything (git add -A + commit). Message goes through a temp file (quoting/multiline safe).</summary>
        public async Task CommitAllAsync(string repoDir, string message, CancellationToken ct = default)
        {
            await RunGitAsync(repoDir, "add -A", ct: ct).ConfigureAwait(false);
            string messageFile = Path.Combine(Path.GetTempPath(), "coab-commit-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(messageFile, message ?? "");
            try
            {
                await RunGitAsync(repoDir, $"commit -F \"{messageFile}\"", ct: ct).ConfigureAwait(false);
            }
            finally
            {
                try { File.Delete(messageFile); } catch { }
            }
        }

        /// <summary>Writes the HEAD version of a file into a temp file for diffing; null when it has no HEAD version.</summary>
        public async Task<string> GetHeadVersionToTempFileAsync(string repoDir, string filePath, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, $"show \"HEAD:{filePath}\""), cancellationToken: ct)
                .ConfigureAwait(false);
            if (!result.Success)
                return null;
            string tempFile = Path.Combine(Path.GetTempPath(), "COAB", "Diff",
                Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
            File.WriteAllText(tempFile, result.StdOut);
            return tempFile;
        }

        public Task CheckoutBranchAsync(string repoDir, string branch, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"checkout \"{branch}\"", ct: ct);
        }

        /// <summary>Creates a branch at HEAD ("git checkout -b" / "git branch").</summary>
        public Task CreateBranchAsync(string repoDir, string name, bool checkout = true, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, checkout ? $"checkout -b \"{name}\"" : $"branch \"{name}\"", ct: ct);
        }

        /// <summary>Deletes a local branch ("git branch -d", with <paramref name="force"/> "-D").</summary>
        public Task DeleteBranchAsync(string repoDir, string name, bool force = false, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"branch {(force ? "-D" : "-d")} \"{name}\"", ct: ct);
        }

        /// <summary>Pushes the current branch; with <paramref name="setUpstream"/> as "git push -u origin &lt;current&gt;".</summary>
        public async Task PushAsync(string repoDir, Action<string> onOutput = null, bool setUpstream = false, CancellationToken ct = default)
        {
            if (setUpstream)
            {
                var branch = await GetCurrentBranchAsync(repoDir, ct).ConfigureAwait(false);
                await RunGitAsync(repoDir, $"push -u origin \"{branch}\"", onOutput, ct).ConfigureAwait(false);
            }
            else
            {
                await RunGitAsync(repoDir, "push", onOutput, ct).ConfigureAwait(false);
            }
        }

        /// <summary>Ahead/behind of <paramref name="branch"/> (default: current) against its upstream. No upstream: (0,0) with HasUpstream=false.</summary>
        public async Task<BranchSyncStatus> GetAheadBehindAsync(string repoDir, string branch = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(branch))
                branch = await GetCurrentBranchAsync(repoDir, ct).ConfigureAwait(false);

            var upstream = await ProcessRunner.RunAsync("git",
                GitArgs(repoDir, $"rev-parse --abbrev-ref \"{branch}@{{upstream}}\""), cancellationToken: ct).ConfigureAwait(false);
            if (!upstream.Success)
                return new BranchSyncStatus { Branch = branch, HasUpstream = false };

            var result = await RunGitAsync(repoDir,
                $"rev-list --left-right --count \"{branch}...{upstream.StdOut.Trim()}\"", ct: ct).ConfigureAwait(false);
            var parts = result.StdOut.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new BranchSyncStatus
            {
                Branch = branch,
                HasUpstream = true,
                Ahead = parts.Length > 0 ? int.Parse(parts[0]) : 0,
                Behind = parts.Length > 1 ? int.Parse(parts[1]) : 0
            };
        }

        /// <summary>HEAD commit sha, or null when unavailable (e.g. empty repository).</summary>
        public async Task<string> GetRevisionAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, "rev-parse HEAD"), cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? result.StdOut.Trim() : null;
        }

        /// <summary>Stashes uncommitted changes when <paramref name="enabled"/> and the tree is dirty. True when a stash was created.</summary>
        public async Task<bool> AutoStashAsync(string repoDir, bool enabled, CancellationToken ct = default)
        {
            if (!enabled)
                return false;
            var status = await GetStatusAsync(repoDir, ct).ConfigureAwait(false);
            if (status.Count == 0)
                return false;
            await StashPushAsync(repoDir, "coab-auto", ct).ConfigureAwait(false);
            return true;
        }

        /// <summary>Restores an auto-stash. False when the pop conflicts — the changes then stay safely in stash@{0}.</summary>
        public async Task<bool> TryAutoStashPopAsync(string repoDir, CancellationToken ct = default)
        {
            try
            {
                await StashPopAsync(repoDir, 0, ct).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Latest tag reachable from HEAD ("git describe --tags --abbrev=0"), or null when the repository has no tags.</summary>
        public async Task<string> GetLastTagAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, "describe --tags --abbrev=0"), cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? result.StdOut.Trim() : null;
        }

        /// <summary>Commit subjects since the latest tag (or the last 50 without tags) as markdown bullet lines.</summary>
        public async Task<string> GetChangelogAsync(string repoDir, CancellationToken ct = default)
        {
            string tag = await GetLastTagAsync(repoDir, ct).ConfigureAwait(false);
            string range = tag == null ? "--max-count=50" : $"\"{tag}..HEAD\"";
            var result = await RunGitAsync(repoDir, $"log {range} --no-merges --format=\"- %s\"", ct: ct).ConfigureAwait(false);
            string header = tag == null ? "## Changes (last 50 commits)" : $"## Changes since {tag}";
            return header + Environment.NewLine + Environment.NewLine + result.StdOut.Trim() + Environment.NewLine;
        }

        /// <summary>Configured "git config user.name" or null when unset.</summary>
        public async Task<string> GetConfiguredUserAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, "config user.name"), cancellationToken: ct).ConfigureAwait(false);
            return result.Success ? result.StdOut.Trim() : null;
        }

        public async Task<IReadOnlyList<GitStash>> GetStashesAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, "stash list --format=" + StashListFormat, ct: ct).ConfigureAwait(false);
            var stashes = new List<GitStash>();
            foreach (var line in SplitLines(result.StdOut))
            {
                var parts = line.Split('\t');
                if (parts.Length < 5)
                    continue;
                var stash = new GitStash(line)
                {
                    GitDirectory = repoDir,
                    Hash = parts[0],
                    Id = parts[1],
                    TimeInfo = parts[2],
                    Creator = parts[3]
                };
                var subject = parts[4];
                var colon = subject.IndexOf(':');
                if (colon > 0)
                {
                    var head = subject.Substring(0, colon);
                    stash.Branch = head.Substring(head.LastIndexOf(' ') + 1);
                    stash.Name = subject.Substring(colon + 1).Trim();
                }
                else
                {
                    stash.Name = subject;
                }
                stashes.Add(stash);
            }
            return stashes;
        }

        public Task StashPushAsync(string repoDir, string message = null, CancellationToken ct = default)
        {
            var args = string.IsNullOrEmpty(message) ? "stash push" : $"stash push -m \"{message}\"";
            return RunGitAsync(repoDir, args, ct: ct);
        }

        public Task StashApplyAsync(string repoDir, int index, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"stash apply \"stash@{{{index}}}\"", ct: ct);
        }

        public Task StashPopAsync(string repoDir, int index, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"stash pop \"stash@{{{index}}}\"", ct: ct);
        }

        public Task StashDropAsync(string repoDir, int index, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"stash drop \"stash@{{{index}}}\"", ct: ct);
        }

        /// <summary>Patch of a stash ("git stash show -p stash@{index}").</summary>
        public async Task<string> GetStashDiffAsync(string repoDir, int index, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, $"stash show -p \"stash@{{{index}}}\"", ct: ct).ConfigureAwait(false);
            return result.StdOut;
        }

        private const string LogFormat = "%H%x09%h%x09%an%x09%ci%x09%s";

        /// <summary>Latest commits of the current branch, newest first ("git log"), optionally filtered by author/age/message.</summary>
        public async Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string repoDir, int maxCount = 100,
            string author = null, int? sinceDays = null, string grep = null, string pathFilter = null, CancellationToken ct = default)
        {
            var args = $"log --max-count={maxCount} --format={LogFormat}";
            if (!string.IsNullOrEmpty(author))
                args += $" -i --author=\"{author}\"";
            if (sinceDays.HasValue)
                args += $" --since={sinceDays.Value}.days";
            if (!string.IsNullOrEmpty(grep))
                args += $" -i --grep=\"{grep}\"";
            if (!string.IsNullOrEmpty(pathFilter))
                args += $" --follow -- \"{pathFilter}\"";
            var result = await RunGitAsync(repoDir, args, ct: ct).ConfigureAwait(false);
            var commits = new List<GitCommit>();
            foreach (var line in SplitLines(result.StdOut))
            {
                var parts = line.Split(new[] { '\t' }, 5);
                if (parts.Length < 5)
                    continue;
                commits.Add(new GitCommit
                {
                    Sha = parts[0],
                    ShortSha = parts[1],
                    Author = parts[2],
                    Date = parts[3],
                    Message = parts[4]
                });
            }
            return commits;
        }

        /// <summary>Commit details with file stat ("git show --stat").</summary>
        public async Task<string> GetCommitDetailsAsync(string repoDir, string sha, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, $"show --stat \"{sha}\"", ct: ct).ConfigureAwait(false);
            return result.StdOut;
        }

        /// <summary>Files changed by a commit ("git show --name-status").</summary>
        public async Task<IReadOnlyList<GitCommitFile>> GetCommitFilesAsync(string repoDir, string sha, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, $"show --name-status --format= \"{sha}\"", ct: ct).ConfigureAwait(false);
            var files = new List<GitCommitFile>();
            foreach (var line in SplitLines(result.StdOut))
            {
                var parts = line.Split('\t');
                if (parts.Length < 2)
                    continue;
                files.Add(new GitCommitFile(parts[0].Substring(0, 1), parts[parts.Length - 1]));
            }
            return files;
        }

        /// <summary>Diff of a single file within a commit ("git show &lt;sha&gt; -- &lt;file&gt;").</summary>
        public async Task<string> GetFileDiffAsync(string repoDir, string sha, string filePath, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, $"show --format= \"{sha}\" -- \"{filePath}\"", ct: ct).ConfigureAwait(false);
            return result.StdOut;
        }

        /// <summary>Working-tree status ("git status --porcelain=v1 -z").</summary>
        public async Task<IReadOnlyList<GitChange>> GetStatusAsync(string repoDir, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, "status --porcelain=v1 -z -uall", ct: ct).ConfigureAwait(false);
            return ParseStatus(result.StdOut);
        }

        internal static IReadOnlyList<GitChange> ParseStatus(string output)
        {
            var changes = new List<GitChange>();
            var entries = output.Split('\0');
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i].Trim('\r', '\n');
                if (entry.Length < 4)
                    continue;
                char x = entry[0];
                char y = entry[1];
                string path = entry.Substring(3);

                if (x == 'R' || x == 'C' || y == 'R' || y == 'C')
                    i++;

                if (x == '!' && y == '!')
                    continue;

                if (x == '?' && y == '?')
                {
                    changes.Add(new GitChange(path, GitChangeType.Untracked, isStaged: false));
                }
                else if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D'))
                {
                    changes.Add(new GitChange(path, GitChangeType.Conflicted, isStaged: false));
                }
                else
                {
                    if (x != ' ')
                        changes.Add(new GitChange(path, MapChangeType(x), isStaged: true));
                    if (y != ' ')
                        changes.Add(new GitChange(path, MapChangeType(y), isStaged: false));
                }
            }
            return changes;
        }

        private static GitChangeType MapChangeType(char code)
        {
            switch (code)
            {
                case 'A': return GitChangeType.Added;
                case 'D': return GitChangeType.Deleted;
                case 'R':
                case 'C': return GitChangeType.Renamed;
                default: return GitChangeType.Modified;
            }
        }

        /// <summary>Diff of the working tree (or index with <paramref name="staged"/>), optionally for a single file.</summary>
        public async Task<string> GetDiffAsync(string repoDir, string filePath = null, bool staged = false, CancellationToken ct = default)
        {
            var args = staged ? "diff --cached" : "diff";
            if (!string.IsNullOrEmpty(filePath))
                args += $" -- \"{filePath}\"";
            var result = await RunGitAsync(repoDir, args, ct: ct).ConfigureAwait(false);
            return result.StdOut;
        }

        /// <summary>Writes "git diff HEAD" to a .patch file. Tracked changes only — untracked files are covered by the zip export.</summary>
        public async Task ExportChangesAsPatchAsync(string repoDir, string targetFile, CancellationToken ct = default)
        {
            var result = await RunGitAsync(repoDir, "diff HEAD", ct: ct).ConfigureAwait(false);
            File.WriteAllText(targetFile, result.StdOut);
        }

        /// <summary>Zips all changed + untracked files (except deletions) with their repo-relative folder structure.</summary>
        public async Task ExportChangesAsZipAsync(string repoDir, string targetZip, CancellationToken ct = default)
        {
            var changes = await GetStatusAsync(repoDir, ct).ConfigureAwait(false);
            var paths = changes
                .Where(c => c.ChangeType != GitChangeType.Deleted)
                .Select(c => c.FilePath)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            using (var stream = File.Create(targetZip))
            using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var relative in paths)
                {
                    ct.ThrowIfCancellationRequested();
                    var fullPath = Path.Combine(repoDir, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                        System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip, fullPath, relative);
                }
            }
        }

        private static string GitArgs(string repoDir, string args)
        {
            return $"-C \"{repoDir}\" {args}";
        }

        private static async Task<ProcessResult> RunGitAsync(string repoDir, string args, Action<string> onOutput = null, CancellationToken ct = default)
        {
            var result = await ProcessRunner.RunAsync("git", GitArgs(repoDir, args),
                onOutputLine: onOutput, cancellationToken: ct).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"git {args} in \"{repoDir}\" failed with exit code {result.ExitCode}: {result.StdErr.Trim()}");
            return result;
        }

        private static IEnumerable<string> SplitLines(string output)
        {
            return output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0);
        }
    }
}
