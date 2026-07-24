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

        public Task CheckoutBranchAsync(string repoDir, string branch, CancellationToken ct = default)
        {
            return RunGitAsync(repoDir, $"checkout \"{branch}\"", ct: ct);
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
