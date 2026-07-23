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
