using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

		protected override async Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			var buildSettings = GetSettings<BuildServiceSettings>(settings);
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

		public async Task<BuildResult> BuildSolutionAsync(ISolutionProjectModel model, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);

			model.CurrentOperation = Operations.BuildIndeterminate;
			try
			{
				GetBuildCommand(model, settings, out string exe, out string args);
				var errors = new List<BuildError>();
				var result = await ProcessRunner.RunAsync(exe, args, model.SolutionFolder,
					line => ParseErrorLine(line, model, errors),
					cancellationToken: cancellation.Token).ConfigureAwait(false);

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

		private static void GetBuildCommand(ISolutionProjectModel model, IServiceSettings settings, out string exe, out string args)
		{
			var buildSettings = GetSettings<BuildServiceSettings>(settings, model);
			string targets = string.Join(";", model.BuildTargets ?? new[] { "Build" });
			var properties = new StringBuilder();
			foreach (var pair in model.BuildProperties ?? new Dictionary<string, string>())
				properties.Append($" /p:{pair.Key}=\"{pair.Value}\"");

			string msbuild = VsWhere.MsBuildPath;
			if (msbuild != null)
			{
				int nodes = Math.Max(1, buildSettings.MaxNodeCount);
				exe = msbuild;
				args = $"\"{model.ItemPath}\" /restore /t:{targets} /v:m /m:{nodes} /nr:{buildSettings.EnableNodeReuse.ToString().ToLowerInvariant()}{properties}";
			}
			else
			{
				exe = "dotnet";
				args = $"build \"{model.ItemPath}\"{properties}";
			}
		}
	}
}
