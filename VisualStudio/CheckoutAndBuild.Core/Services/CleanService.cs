using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>Deletes output/intermediate paths and configured custom clean paths (port of CleanupService).</summary>
	public class CleanService : OperationServiceBase
	{
		public override Guid ServiceId => new Guid(ServiceIds.CleanServiceId);
		public override int Order => ServicePriorities.CleanupServicePriority;
		public override string OperationName => "Clean";

		protected override async Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			foreach (var solution in solutionProjects)
			{
				await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);
				if (IsCancelled(solution))
					continue;

				solution.CurrentOperation = Operations.Clean;
				solution.ResetProgress();
				var errors = new List<Exception>();
				try
				{
					foreach (string path in GetCleanPaths(solution, settings))
					{
						await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);
						TryDelete(path, errors);
						solution.IncrementProgress();
					}
				}
				finally
				{
					solution.CurrentOperation = Operations.None;
				}

				if (errors.Count > 0)
					solution.SetResult(new AggregateException(
						$"Clean of {solution.SolutionFileName} finished with {errors.Count} error(s).", errors));
			}
		}

		public override string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType)
		{
			var builder = new StringBuilder();
			var paths = models.SelectMany(model => GetCleanPaths(model, settings)).Distinct(StringComparer.OrdinalIgnoreCase);
			foreach (string path in paths)
			{
				if (scriptExportType == ScriptExportType.Batch)
					builder.AppendLine($"rmdir /s /q \"{path}\"");
				else
					builder.AppendLine($"If (Test-Path '{path}') {{ Remove-Item '{path}' -Recurse -Force }}");
			}
			return builder.ToString();
		}

		private static IEnumerable<string> GetCleanPaths(ISolutionProjectModel solution, IServiceSettings settings)
		{
			var paths = new List<string>();

			var customPaths = GetSettings<CleanServiceSettings>(settings, solution).CustomCleanPathes ?? new string[0];
			foreach (string entry in customPaths.SelectMany(p => (p ?? string.Empty).Split(';')))
			{
				string path = entry.Trim();
				if (path.Length == 0)
					continue;
				paths.Add(Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(solution.SolutionFolder, path)));
			}

			foreach (var project in GetProjectInfos(solution))
			{
				if (!string.IsNullOrEmpty(project.OutputPath))
					paths.Add(project.OutputPath);
				if (!string.IsNullOrEmpty(project.IntermediateOutputPath))
					paths.Add(project.IntermediateOutputPath);
			}

			return paths.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		private static void TryDelete(string path, ICollection<Exception> errors)
		{
			try
			{
				if (Directory.Exists(path))
					Directory.Delete(path, true);
				else if (File.Exists(path))
					File.Delete(path);
			}
			catch (Exception e)
			{
				errors.Add(new IOException($"Could not delete \"{path}\": {e.Message}", e));
			}
		}
	}
}
