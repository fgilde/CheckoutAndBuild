using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CheckoutAndBuild.Core.Execution
{
	/// <summary>
	/// Finds/kills processes whose executable lives beneath given directories —
	/// used to stop built executables and to free locked build outputs
	/// (old "Kill dependent processes" behavior).
	/// </summary>
	public static class RunningProcessHelper
	{
		/// <summary>Running processes whose main module path starts with one of the directories. Inaccessible processes are skipped.</summary>
		public static IReadOnlyList<Process> FindProcessesInDirectories(IEnumerable<string> directories)
		{
			var roots = directories
				.Where(d => !string.IsNullOrEmpty(d))
				.Select(d => Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
				.ToList();
			var found = new List<Process>();
			if (roots.Count == 0)
				return found;

			foreach (var process in Process.GetProcesses())
			{
				string path;
				try
				{
					path = process.MainModule?.FileName;
				}
				catch (Exception)
				{
					continue;
				}
				if (path != null && roots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
					found.Add(process);
			}
			return found;
		}

		/// <summary>Kills all processes under the directories; returns how many were killed.</summary>
		public static int KillProcessesInDirectories(IEnumerable<string> directories)
		{
			int killed = 0;
			foreach (var process in FindProcessesInDirectories(directories))
			{
				try
				{
					process.Kill();
					process.WaitForExit(5000);
					killed++;
				}
				catch (Exception)
				{
				}
			}
			return killed;
		}
	}
}
