using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using CheckoutAndBuild.Core.Execution;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>Locates VS tools (msbuild.exe, vstest.console.exe) via vswhere.exe; results are cached.</summary>
	internal static class VsWhere
	{
		private static readonly ConcurrentDictionary<string, string> cache =
			new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Full path to msbuild.exe of the latest VS installation, or null.</summary>
		public static string MsBuildPath =>
			Find(@"-latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe");

		/// <summary>Full path to vstest.console.exe of the latest VS installation, or null.</summary>
		public static string VsTestPath =>
			Find(@"-latest -find Common7\IDE\Extensions\TestPlatform\vstest.console.exe");

		private static string Find(string args)
		{
			return cache.GetOrAdd(args, a =>
			{
				string vswhere = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
					"Microsoft Visual Studio", "Installer", "vswhere.exe");
				if (!File.Exists(vswhere))
					return null;
				var result = ProcessRunner.RunAsync(vswhere, a).GetAwaiter().GetResult();
				if (!result.Success)
					return null;
				string first = result.StdOut
					.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.FirstOrDefault();
				return !string.IsNullOrEmpty(first) && File.Exists(first) ? first : null;
			});
		}
	}
}
