using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheckoutAndBuild2.Contracts;
using EnvDTE80;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using nExt.Core.Extensions;
using nExt.Core.Helper;

namespace FG.CheckoutAndBuild2.Git
{
    public static class GitHelper
    {
        private static string AsOneLine(this string s)
        {
            return s.Replace(Environment.NewLine, "").Replace(@"\n", "").Replace("\n", "");
        }

        public static IEnumerable<string> GetBranches(string directory)
        {
            var cmd = $"git";
            var args = $"-C \"{directory}\" branch";

            IEnumerable<string> result = Enumerable.Empty<string>();
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, s =>
            {
                if (!string.IsNullOrEmpty(s))
                    result = s.Split('\n').Select(s1 => s1.AsOneLine().Replace("*", "").TrimEnd().TrimStart());
            });
            return result.Where(s => !string.IsNullOrEmpty(s));
        }

        public static IEnumerable<FileInfo> GetAllFiles(string directory, params string[] extensions)
        {
            IEnumerable<FileInfo> result = Enumerable.Empty<FileInfo>();
            var folder = GetTopLevelRepoDir(directory);
            if (folder != null && folder.Exists)
            {
                var cmd = $"git";
                var args = $"-C \"{folder.FullName}\" ls-files ";
                if (extensions != null && extensions.Any())
                    args += string.Join(" ", extensions.Select(s => $"{s}"));
                ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, s =>
                {
                    result = s.Split('\n').Select(s1 => new FileInfo(Path.Combine(folder.FullName, s1)));
                });
            }
            return result; //result.Where(info => info.Exists);
        }

        public static FileInfo GetRepoHeadFile(string directory)
        {
            var repoDir = GetTopLevelRepoDir(directory);
            return repoDir != null ? new FileInfo(Path.Combine(repoDir.FullName, ".git", "HEAD")) : null;
        }

        public static DirectoryInfo GetTopLevelRepoDir(string directory)
        {
            var cmd = $"git";
            var args = $"-C \"{directory}\" rev-parse --show-toplevel";
            string res = "";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, s => { res = s.AsOneLine(); });
            if (!string.IsNullOrEmpty(res))
            {
                var result = new DirectoryInfo(res);
                return result;
            }
            return null;
        }

        public static bool IsGitControlled(string directory)
        {
            return !string.IsNullOrEmpty(GetCurrentBranchName(directory));
        }

        public static string GetCurrentBranchName(string directory)
        {
            return GetCurrentBranch(directory).Split('/').LastOrDefault();
        }

        public static string SetCurrentBranch(string directory, string branchName)
        {
            var cmd = $"git";
            var args = $"-C \"{directory}\" checkout {branchName}";
            string res = "";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream);
            return GetCurrentBranch(directory);
        }

        public static string GetCurrentBranch(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return string.Empty;
            try
            {
                var cmd = $"git";
                var args = $"-C \"{directory}\" rev-parse --abbrev-ref HEAD";
                string res = "";
                ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, s =>
                {
                    res = s.AsOneLine();
                });
                var result = res.AsOneLine();
                return result;
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Microsoft.TeamFoundation.Build.Client.GitHelper
        public static IEnumerable<GitRepository> GetGitRepositories()
        {
            List<GitRepository> result = new List<GitRepository>();
            const string registryKey = @"Software\Microsoft\VisualStudio\{0}\TeamFoundation\GitSourceControl\Repositories\";
            var dte = CheckoutAndBuild2Package.GetGlobalService<DTE2>();
            string rootSuffix;

            IVsAppCommandLine vsAppCommandLine = CheckoutAndBuild2Package.GetGlobalService<IVsAppCommandLine>();
            int pos;
            vsAppCommandLine.GetOption("rootsuffix", out pos, out rootSuffix);

            string currentStudioInfo = dte.Version + rootSuffix;

            using (var repoKey = Registry.CurrentUser.OpenSubKey(string.Format(registryKey, currentStudioInfo)))
            {
                if (repoKey != null)
                {
                    foreach (string subKeyName in repoKey.GetSubKeyNames())
                    {
                        using (var detailKey = repoKey.OpenSubKey(subKeyName))
                        {
                            if (detailKey != null)
                            {
                                var name = detailKey.GetValue("Name");
                                var path = detailKey.GetValue("Path");
                                if (name != null && path != null)
                                {
                                    var info = new GitRepository(name.ToString(), path.ToString());
                                    if (!result.Contains(info))
                                        result.Add(info);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }


        public static bool HasChanges(string directory)
        {
            var cmd = $"git";
            var args = $"-C \"{directory}\" diff";
            string res = "";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, s =>
            {
                res = s.AsOneLine();
            });
            var result = res.AsOneLine();
            return !string.IsNullOrEmpty(result);
        }

        public static void CreateGitPatch(string gitWorkingDirectory, string fullPatchFilePath, bool binary)
        {
            string bin = binary ? "--binary" : "";
            var cmd = $"git";
            var args = $"-C \"{gitWorkingDirectory}\" diff {bin} > \"{fullPatchFilePath}\"";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.DefaultWithCmd, s => Output.Notification(s), s => Output.Notification(s, NotificationType.Error));
        }

        public static void Push(string gitWorkingDirectory, bool force)
        {
            string f = force ? "-f" : "";
            var cmd = $"cmd";
            var args = $"/c git -C \"{gitWorkingDirectory}\" push {f}";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, Output.NotificationInfo, Output.NotificationError);
        }

        public static void ApplyGitPatch(string gitWorkingDirectory, string fullPatchFilePath)
        {
            var cmd = $"git";
            var args = $"-C \"{gitWorkingDirectory}\" apply \"{fullPatchFilePath}\"";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.DefaultWithCmd, Output.NotificationInfo, Output.NotificationError);
        }

        public static void ApplyStash(GitStash stash)
        {            
            var cmd = "git";
            var args = $"-C \"{stash.GitDirectory}\" stash apply \"{stash.Id}\"";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, Output.NotificationInfo, Output.NotificationError);            
        }

        public static GitStash CreateStash(string gitWorkingDirectory, string stashName)
        {
            GitStash res = null;
            var cmd = "git";            
            var args = $"-C \"{gitWorkingDirectory}\" stash save \"{stashName}\"";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.Default, s => { res = GetStashes(gitWorkingDirectory).FirstOrDefault(stash => stash.Name == stashName); }, Output.NotificationError);            
            return res;
        }

        public static GitStashInfo GetStashDetails(GitStash stash)
        {
            GitStashInfo res = null;
            var cmd = "git";            
            var args = $"-C \"{stash.GitDirectory}\" stash show {stash.Id}";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, s =>
            {
                res = GitStashInfo.ParseFromCmdResult(s, stash.GitDirectory);
                EnumerableExtensions.Apply(res.Changes, change => change.Image = FileHelper.GetFileIconForExtensionOrFilename(Path.GetExtension(change.FileName)).ToBitmap().ToImageSource());
                res.Stash = stash;
            });
            return res;
        }

        public static IEnumerable<GitStash> GetStashes(string gitWorkingDirectory)
        {
            var res = new List<GitStash>();
            var cmd = "git";
            string format = "--pretty=format:\"%h:%gd:%s:%cr:%an";
            var args = $"-C \"{gitWorkingDirectory}\" stash list {format}";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.Default, s =>
            {
                var stash = GitStash.ParseFromCmdResultLine(s, format);
                if (stash != null)
                {
                    stash.GitDirectory = gitWorkingDirectory;
                    res.Add(stash);
                }
            });
            return res;
        }

        public static void CreateBranchForStash(GitStash stash, string branchname)
        {
            var cmd = "git";
            var args = $"-C \"{stash.GitDirectory}\" stash branch  \"{branchname}\"";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, Output.NotificationInfo, Output.NotificationError);
        }

        public static void DeleteStash(GitStash stash)
        {
            var cmd = "git";
            var args = $"-C \"{stash.GitDirectory}\" stash drop  \"{stash.Id}\"";
            ScriptHelper.ExecuteScript(cmd, args, ScriptExecutionSettings.OneOutputStream, Output.NotificationInfo, Output.NotificationError);
        }
    }
}