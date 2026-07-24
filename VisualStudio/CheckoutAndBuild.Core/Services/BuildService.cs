using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>One parsed msbuild error or warning line.</summary>
	public sealed class BuildError
	{
		public string File { get; set; }
		public int Line { get; set; }
		public int Column { get; set; }
		public string Code { get; set; }
		public string Message { get; set; }
		public bool IsWarning { get; set; }
		public string Solution { get; set; }

		public override string ToString() =>
			$"{File}({Line},{Column}): {(IsWarning ? "warning" : "error")} {Code}: {Message}";
	}

	/// <summary>Result of building one solution.</summary>
	public sealed class BuildResult
	{
		public bool Success { get; set; }
		public IReadOnlyList<BuildError> Errors { get; set; } = new BuildError[0];
	}

	/// <summary>
	/// Builds the solutions out-of-process via msbuild.exe (located through vswhere, falling back
	/// to "dotnet build"). Port of LocalBuildService without the in-process MSBuild API.
	/// </summary>
	public class BuildService : OperationServiceBase
	{
		// e.g. C:\x\Foo.cs(12,5): error CS1002: ; expected [C:\x\Foo.csproj]
		private static readonly Regex errorLine = new Regex(
			@"^\s*(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<msg>.*?)(\s*\[[^\]]+\])?\s*$",
			RegexOptions.Compiled);

		public override Guid ServiceId => new Guid(ServiceIds.BuildServiceId);
		public override int Order => ServicePriorities.BuildServicePriority;
		public override string OperationName => "Build";

		/// <summary>Plugin providers contributing extra default /p: properties per project (wired by the host; default: none). Explicit model properties win.</summary>
		public IReadOnlyCollection<IProjectBuildPropertiesProvider> BuildPropertiesProviders { get; set; }

		protected override async Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			var buildSettings = GetSettings<BuildServiceSettings>(settings);

			if (buildSettings.BuildMode == BuildMode.MergedBuild && solutionProjects.Count > 1)
			{
				await BuildMergedAsync(solutionProjects, settings, buildSettings, cancellation).ConfigureAwait(false);
				return;
			}

			var groups = solutionProjects
				.GroupBy(p => p.BuildPriority)
				.OrderBy(g => g.Key)
				.ToList();

			foreach (var group in groups)
			{
				await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);

				var results = await Task.WhenAll(group
					.Where(model => !IsCancelled(model))
					.Select(model => BuildSolutionAsync(model, settings, cancellation))).ConfigureAwait(false);

				if (buildSettings.CancelQueuedOnFailures && results.Any(r => !r.Success))
					break;
			}
		}

		/// <summary>Merges all solutions into one temporary solution and builds that (old MergedBuild mode, dependency order via msbuild).</summary>
		private async Task BuildMergedAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings,
			BuildServiceSettings buildSettings, PausableCancellationTokenSource cancellation)
		{
			string mergedPath = Path.Combine(solutionProjects[0].SolutionFolder,
				"!Merged_Build_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".sln");
			Merge.SolutionMerger.Merge(solutionProjects.Select(p => p.ItemPath), mergedPath);
			CoabLog.Info($"MergedBuild: created {mergedPath}");

			foreach (var model in solutionProjects)
				model.CurrentOperation = Operations.BuildIndeterminate;
			try
			{
				KillDependentProcessesIfConfigured(solutionProjects, buildSettings);
				var errors = new List<BuildError>();
				GetBuildCommandForPath(mergedPath, solutionProjects[0], settings, out string exe, out string args);
				var result = await ProcessRunner.RunAsync(exe, args, Path.GetDirectoryName(mergedPath),
					line => { CoabLog.Detail(line); ParseErrorLine(line, solutionProjects[0], errors); },
					cancellationToken: cancellation.Token,
					priority: GetProcessPriority(buildSettings)).ConfigureAwait(false);

				var buildResult = new BuildResult { Success = result.Success, Errors = errors };
				foreach (var model in solutionProjects)
					model.SetResult(buildResult);
				CoabLog.Info($"MergedBuild {(result.Success ? "succeeded" : "failed")} ({errors.Count(e => !e.IsWarning)} error(s)). Merged solution kept at {mergedPath}");
			}
			finally
			{
				foreach (var model in solutionProjects)
					model.CurrentOperation = Operations.None;
			}
		}

		public async Task<BuildResult> BuildSolutionAsync(ISolutionProjectModel model, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);

			var buildSettings = GetSettings<BuildServiceSettings>(settings, model);
			model.CurrentOperation = Operations.BuildIndeterminate;
			try
			{
				KillDependentProcessesIfConfigured(new[] { model }, buildSettings);
				CoabLog.Info($"Building {model.SolutionFileName}...");
				GetBuildCommand(model, settings, out string exe, out string args);
				var errors = new List<BuildError>();
				var result = await ProcessRunner.RunAsync(exe, args, model.SolutionFolder,
					line => { CoabLog.Detail(line); ParseErrorLine(line, model, errors); },
					cancellationToken: cancellation.Token,
					priority: GetProcessPriority(buildSettings)).ConfigureAwait(false);

				var buildResult = new BuildResult
				{
					Success = result.Success,
					Errors = errors
				};
				model.SetResult(buildResult);
				return buildResult;
			}
			finally
			{
				model.CurrentOperation = Operations.None;
			}
		}

		public override string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType)
		{
			var builder = new StringBuilder();
			foreach (var model in models.OrderBy(m => m.BuildPriority))
			{
				GetBuildCommand(model, settings, out string exe, out string args);
				if (scriptExportType == ScriptExportType.Powershell && exe != "dotnet")
					builder.AppendLine($"& '{exe}' {args}");
				else if (exe == "dotnet")
					builder.AppendLine($"dotnet {args}");
				else
					builder.AppendLine($"\"{exe}\" {args}");
			}
			return builder.ToString();
		}

		private static void ParseErrorLine(string line, ISolutionProjectModel model, List<BuildError> errors)
		{
			var match = errorLine.Match(line ?? string.Empty);
			if (!match.Success)
				return;
			lock (errors)
			{
				errors.Add(new BuildError
				{
					File = match.Groups["file"].Value.Trim(),
					Line = int.Parse(match.Groups["line"].Value),
					Column = int.Parse(match.Groups["col"].Value),
					Code = match.Groups["code"].Value,
					Message = match.Groups["msg"].Value,
					IsWarning = match.Groups["sev"].Value == "warning",
					Solution = model.SolutionFileName
				});
			}
		}

		private void GetBuildCommand(ISolutionProjectModel model, IServiceSettings settings, out string exe, out string args)
			=> GetBuildCommandForPath(model.ItemPath, model, settings, out exe, out args);

		private void GetBuildCommandForPath(string itemPath, ISolutionProjectModel model, IServiceSettings settings, out string exe, out string args)
		{
			var buildSettings = GetSettings<BuildServiceSettings>(settings, model);
			string targets = string.Join(";", model.BuildTargets ?? new[] { "Build" });

			var merged = new Dictionary<string, string>();
			foreach (var provider in BuildPropertiesProviders ?? Enumerable.Empty<IProjectBuildPropertiesProvider>())
			{
				var defaults = provider.GetDefaultBuildProperties(model, settings);
				foreach (var pair in defaults ?? new Dictionary<string, string>())
					merged[pair.Key] = pair.Value;
			}
			foreach (var pair in model.BuildProperties ?? new Dictionary<string, string>())
				merged[pair.Key] = pair.Value;

			var properties = new StringBuilder();
			foreach (var pair in merged)
				properties.Append($" /p:{pair.Key}=\"{pair.Value}\"");

			string verbosity = GetVerbosityFlag(settings);

			if (model.IsDelphiProject && !string.IsNullOrEmpty(settings?.DelphiPath))
			{
				// Delphi: rsvars.bat sets BDS/framework env, then msbuild builds the .dproj/.groupproj (old bds path)
				string rsvars = File.Exists(settings.DelphiPath) && settings.DelphiPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
					? settings.DelphiPath
					: Path.Combine(settings.DelphiPath, "bin", "rsvars.bat");
				exe = "cmd";
				args = $"/s /c \"\"{rsvars}\" && msbuild \"{itemPath}\" /t:{targets} /v:{verbosity}{properties}\"";
				return;
			}

			string msbuild = VsWhere.MsBuildPath;
			if (msbuild != null)
			{
				int nodes = Math.Max(1, buildSettings.MaxNodeCount);
				exe = msbuild;
				args = $"\"{itemPath}\" /restore /t:{targets} /v:{verbosity} /m:{nodes} /nr:{buildSettings.EnableNodeReuse.ToString().ToLowerInvariant()}{properties}";
			}
			else
			{
				exe = "dotnet";
				args = $"build \"{itemPath}\" -v {verbosity}{properties}";
			}
		}

		private static string GetVerbosityFlag(IServiceSettings settings)
		{
			switch (settings?.LogLevel ?? LoggerVerbosity.Minimal)
			{
				case LoggerVerbosity.Quiet: return "q";
				case LoggerVerbosity.Normal: return "n";
				case LoggerVerbosity.Detailed: return "d";
				case LoggerVerbosity.Diagnostic: return "diag";
				default: return "m";
			}
		}

		private static ProcessPriorityClass? GetProcessPriority(BuildServiceSettings buildSettings)
		{
			switch (buildSettings.ThreadPriority)
			{
				case ThreadPriority.Lowest: return ProcessPriorityClass.Idle;
				case ThreadPriority.BelowNormal: return ProcessPriorityClass.BelowNormal;
				case ThreadPriority.AboveNormal: return ProcessPriorityClass.AboveNormal;
				case ThreadPriority.Highest: return ProcessPriorityClass.High;
				default: return null;
			}
		}

		/// <summary>Frees locked outputs before building by killing processes running from the output directories.</summary>
		private static void KillDependentProcessesIfConfigured(IReadOnlyList<ISolutionProjectModel> models, BuildServiceSettings buildSettings)
		{
			if (!buildSettings.KillDependendProcesses)
				return;
			var outputDirs = models.SelectMany(GetOutputDirectories).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			int killed = RunningProcessHelper.KillProcessesInDirectories(outputDirs);
			if (killed > 0)
				CoabLog.Info($"Killed {killed} process(es) locking build output.");
		}

		/// <summary>Existing output directories of all projects of a solution.</summary>
		internal static IReadOnlyList<string> GetOutputDirectories(ISolutionProjectModel solution)
		{
			var model = solution as Model.SolutionProjectModel ?? Model.SolutionParser.Parse(solution.ItemPath);
			return model.Projects
				.Select(p => p.OutputPath)
				.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
	}
}
