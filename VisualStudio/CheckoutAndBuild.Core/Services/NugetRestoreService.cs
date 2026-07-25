using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>Restores NuGet packages per solution via dotnet restore or a configured nuget.exe.</summary>
	public class NugetRestoreService : OperationServiceBase
	{
		public override Guid ServiceId => new Guid(ServiceIds.NugetRestoreServiceId);
		public override int Order => ServicePriorities.NugetRestoreServicePriority;
		public override string OperationName => "Nuget Restore";

		protected override async Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			if (GetSettings<NugetServiceSettings>(settings).RunParallel)
			{
				await Task.WhenAll(solutionProjects.Select(model => RestoreAsync(model, settings, cancellation))).ConfigureAwait(false);
			}
			else
			{
				foreach (var model in solutionProjects.OrderBy(m => m.BuildPriority))
					await RestoreAsync(model, settings, cancellation).ConfigureAwait(false);
			}
		}

		public override string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType)
		{
			var builder = new StringBuilder();
			foreach (var model in models)
			{
				GetRestoreCommand(model, settings, out string exe, out string args);
				if (scriptExportType == ScriptExportType.Powershell && exe != "dotnet")
					builder.AppendLine($"& '{exe}' {args}");
				else if (exe == "dotnet")
					builder.AppendLine($"dotnet {args}");
				else
					builder.AppendLine($"\"{exe}\" {args}");
			}
			return builder.ToString();
		}

		private async Task RestoreAsync(ISolutionProjectModel model, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);
			if (IsCancelled(model))
				return;

			model.CurrentOperation = Operations.NugetRestore;
			try
			{
				var nugetSettings = GetSettings<NugetServiceSettings>(settings, model);
				if (nugetSettings.NugetAction == NugetAction.Reinstall)
					DeleteLocalPackagesFolder(model);

				CoabLog.Info($"NuGet restore {model.SolutionFileName}...");
				GetRestoreCommand(model, settings, out string exe, out string args);
				var result = await ProcessRunner.RunAsync(exe, args, model.SolutionFolder,
					CoabLog.Detail,
					cancellationToken: cancellation.Token).ConfigureAwait(false);
				if (!result.Success)
					model.SetResult(new InvalidOperationException(
						$"NuGet restore of {model.SolutionFileName} failed with exit code {result.ExitCode}: {result.StdErr.Trim()}\n{result.StdOut.Trim()}"));
			}
			finally
			{
				model.CurrentOperation = Operations.None;
			}
		}

		private static void GetRestoreCommand(ISolutionProjectModel model, IServiceSettings settings, out string exe, out string args)
		{
			var nugetSettings = GetSettings<NugetServiceSettings>(settings, model);
			string nugetExe = nugetSettings.NugetExeLocation;
			// Install/InstallAndRestore/Reinstall all end in a restore — "restore" installs missing
			// packages anyway; Reinstall additionally wipes the local packages folder first (see RestoreAsync).
			if (!string.IsNullOrEmpty(nugetExe) && File.Exists(nugetExe))
			{
				exe = nugetExe;
				args = $"restore -NonInteractive \"{model.ItemPath}\"";
			}
			else
			{
				exe = "dotnet";
				args = nugetSettings.NugetAction == NugetAction.Reinstall
					? $"restore --force \"{model.ItemPath}\""
					: $"restore \"{model.ItemPath}\"";
			}
		}

		/// <summary>Reinstall: removes the solution-local packages folder so the restore fetches everything fresh.</summary>
		private static void DeleteLocalPackagesFolder(ISolutionProjectModel model)
		{
			string packagesDir = Path.Combine(model.SolutionFolder, "packages");
			if (!Directory.Exists(packagesDir))
				return;
			try
			{
				Directory.Delete(packagesDir, true);
				CoabLog.Info($"Reinstall: deleted {packagesDir}");
			}
			catch (Exception e)
			{
				CoabLog.Error($"Reinstall: could not delete {packagesDir}: {e.Message}");
			}
		}
	}
}
