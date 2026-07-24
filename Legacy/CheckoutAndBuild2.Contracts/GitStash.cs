using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace CheckoutAndBuild2.Contracts
{
    public class GitStash
    {
        public GitStash(string fullStashResult = "")
        {
            FullName = fullStashResult;
        }

        public static GitStash ParseFromCmdResultLine(string s, string usedFormat)
        {
            if (!string.IsNullOrEmpty(s) && s.Contains(":"))
            {
                var infos = s.Split(':');
                if (infos.Length == usedFormat.Split(':').Length)
                {
                    return new GitStash(s)
                    {
                        Hash = infos[0],
                        Id = infos[1],
                        Branch = new string(infos[2].Skip(3).ToArray()).Trim(),
                        Name = infos[3].Trim(),
                        TimeInfo = infos[4],
                        Creator = infos[5]
                    };
                }
            }
            return null;
        }
        public string GitDirectory { get; set; }
        public string FullName { get; set; }
        public string Hash { get; set; }
        public string Creator { get; set; }
        public string TimeInfo { get; set; }
        public string Name { get; set; }
        public string Branch { get; set; }
        public string Id { get; set; }
    }

    public class GitStashInfo
    {
        public GitStash Stash { get; set; }
        public IEnumerable<GitChange> Changes { get; set; }
        public int FileChanges { get; set; }
        public int Insertions { get; set; }
        public int Deletions { get; set; }
        public string StatusText { get; set; }
        public string FullString { get; set; }

        public static GitStashInfo ParseFromCmdResult(string s, string gitDirectory)
        {
            try
            {
                var result = new GitStashInfo { FullString = s };
                var lines = Regex.Split(s, @"\r?\n|\r").Where(s1 => !string.IsNullOrWhiteSpace(s1)).ToArray();
                result.Changes = lines.Where(s1 => s1.Contains("|")).Select(s1 => GitChange.Parse(s1, gitDirectory)).ToArray();
                result.StatusText = lines.Last();
                var infos = result.StatusText.Split(',');
                result.FileChanges = int.Parse(infos[0].Trim().Split(' ')[0].Trim());
                result.Insertions = int.Parse(infos[1].Trim().Split(' ')[0].Trim());
                result.Deletions = int.Parse(infos[2].Trim().Split(' ')[0].Trim());
                return result;
            }
            catch
            {
                return null;
            }
        }
    }

    public class GitChange
    {
        public FileInfo FileInfo { get; set; }
        public string FileName { get; set; }
        public int Changes { get; set; }
        public int Added { get; set; }
        public int Removed { get; set; }
        public string FullString { get; set; }

        public ImageSource Image { get; set; }

        public string FormattedInfo => $"({Changes}) {FileName}";

        public static GitChange Parse(string s, string gitDirectory)
        {

            try
            {
                var result = new GitChange { FullString = s};
                var infos = s.Split('|');
                result.FileName = infos[0].Trim();
                result.FileInfo = new FileInfo(Path.Combine(gitDirectory, result.FileName));
                result.Changes = int.Parse(infos[1].Trim().Split(' ')[0].Trim());
                result.Added = Regex.Matches(infos[1], Regex.Escape("+")).Count;
                result.Removed = Regex.Matches(infos[1], Regex.Escape("-")).Count;                
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}