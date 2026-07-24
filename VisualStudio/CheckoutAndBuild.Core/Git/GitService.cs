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
        // tab-separated: hash, reflog selector (stash@{n}), ISO date, author, reflog subject ("WIP on <branch>: ..." / "On <branch>: <message>")
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
                // subject: "WIP on <branch>: <sha> <msg>" or "On <branch>: <message>" — first colon splits branch from message
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

        // tab-separated: full hash, short hash, author, ISO date, subject
        private const string LogFormat = "%H%x09%h%x09%an%x09%ci%x09%s";

        /// <summary>Latest commits of the current branch, newest first ("git log"), optionally filtered by author/age/message.</summary>
        public async Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string repoDir, int maxCount = 100,
            string author = null, int? sinceDays = null, string grep = null, CancellationToken ct = default)
        {
            var args = $"log --max-count={maxCount} --format={LogFormat}";
            if (!string.IsNullOrEmpty(author))
                args += $" -i --author=\"{author}\"";
            if (sinceDays.HasValue)
                args += $" --since={sinceDays.Value}.days";
            if (!string.IsNullOrEmpty(grep))
                args += $" -i --grep=\"{grep}\"";
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
                // rename/copy lines are "R100\told\tnew" — status letter only, last path wins
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
            // -uall: list untracked files individually instead of collapsing whole directories
            var result = await RunGitAsync(repoDir, "status --porcelain=v1 -z -uall", ct: ct).ConfigureAwait(false);
            return ParseStatus(result.StdOut);
        }

        internal static IReadOnlyList<GitChange> ParseStatus(string output)
        {
            var changes = new List<GitChange>();
            var entries = output.Split('\0');
            for (int i = 0; i < entries.Length; i++)
            {
                // ProcessRunner appends line breaks per output event; paths never start/end with them
                var entry = entries[i].Trim('\r', '\n');
                if (entry.Length < 4)
                    continue;
                char x = entry[0];
                char y = entry[1];
                string path = entry.Substring(3);

                if (x == 'R' || x == 'C' || y == 'R' || y == 'C')
                    i++; // -z rename/copy: next token is the original path

                if (x == '!' && y == '!')
                    continue; // ignored

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
