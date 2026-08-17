using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CheckoutAndBuild.Core.Execution;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>One installed Visual Studio (for "Open with...").</summary>
	public sealed class VsInstance
	{
		public string DisplayName { get; set; }
		public string ProductPath { get; set; }
	}

	/// <summary>Locates VS tools (msbuild.exe, vstest.console.exe) via vswhere.exe; results are cached.</summary>
	public static class VsWhere
	{
		private static IReadOnlyList<VsInstance> instances;

		/// <summary>All installed VS instances (cached). Empty when vswhere is missing.</summary>
		public static IReadOnlyList<VsInstance> GetInstances()
		{
			if (instances != null)
				return instances;
			try
			{
				string vswhere = VsWherePath;
				if (!File.Exists(vswhere))
					return instances = new VsInstance[0];
				var result = ProcessRunner.RunAsync(vswhere, "-all -prerelease -format json").GetAwaiter().GetResult();
				if (!result.Success)
					return instances = new VsInstance[0];
				using (var doc = JsonDocument.Parse(result.StdOut))
				{
					instances = doc.RootElement.EnumerateArray()
						.Select(e => new VsInstance
						{
							DisplayName = e.TryGetProperty("displayName", out var name) ? name.GetString() : "Visual Studio",
							ProductPath = e.TryGetProperty("productPath", out var path) ? path.GetString() : null
						})
						.Where(i => !string.IsNullOrEmpty(i.ProductPath) && File.Exists(i.ProductPath))
						.ToList();
				}
			}
			catch (Exception)
			{
				instances = new VsInstance[0];
			}
			return instances;
		}

		private static string VsWherePath => Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
			"Microsoft Visual Studio", "Installer", "vswhere.exe");
		private static readonly ConcurrentDictionary<string, string> cache =
			new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public static string MsBuildPath =>
			Find(@"-latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe");

		public static string VsTestPath =>
			Find(@"-latest -find Common7\IDE\Extensions\TestPlatform\vstest.console.exe");

		private static string Find(string args)
		{
			return cache.GetOrAdd(args, a =>
			{
				string vswhere = VsWherePath;
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
