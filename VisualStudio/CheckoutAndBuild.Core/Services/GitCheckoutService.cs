using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Git;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.Core.Services
{
	/// <summary>Pulls the git repositories of the solutions (port of the git path of CheckoutService).</summary>
	public class GitCheckoutService : OperationServiceBase
	{
		private readonly GitService git = new GitService();

		public override Guid ServiceId => new Guid(ServiceIds.CheckoutServiceId);
		public override int Order => ServicePriorities.CheckoutServicePriority;
		public override string OperationName => "Checkout";

		protected override async Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			var groups = GroupByRepositoryRoot(solutionProjects, dir => git.GetRepositoryRoot(dir));
			foreach (var group in groups)
			{
				await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);

				var models = group.Value.Where(m => !IsCancelled(m)).ToList();
				if (models.Count == 0)
					continue;

				var checkoutSettings = GetSettings<CheckoutServiceSettings>(settings, models[0]);
				foreach (var model in models)
					model.CurrentOperation = Operations.Checkout;
				try
				{
					if (checkoutSettings.ForceAndOverwrite)
					{
						await RunGitAsync(group.Key, "reset --hard", cancellation.Token).ConfigureAwait(false);
						await RunGitAsync(group.Key, "clean -fd", cancellation.Token).ConfigureAwait(false);
					}
					await git.PullAsync(group.Key, ct: cancellation.Token).ConfigureAwait(false);
				}
				catch (Exception e) when (!(e is OperationCanceledException))
				{
					foreach (var model in models)
						model.SetResult(e);
				}
				finally
				{
					foreach (var model in models)
						model.CurrentOperation = Operations.None;
				}
			}
		}

		public override string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType)
		{
			var builder = new StringBuilder();
			var groups = GroupByRepositoryRoot(models, dir => git.GetRepositoryRoot(dir));
			foreach (string repo in groups.Keys)
				builder.AppendLine($"git -C \"{repo}\" pull");
			return builder.ToString();
		}

		/// <summary>
		/// Groups the git-controlled solutions by repository root (case-insensitive, deduplicated)
		/// so every repository is pulled exactly once.
		/// </summary>
		public static IReadOnlyDictionary<string, List<ISolutionProjectModel>> GroupByRepositoryRoot(
			IEnumerable<ISolutionProjectModel> models, Func<string, string> getRepositoryRoot)
		{
			var groups = new Dictionary<string, List<ISolutionProjectModel>>(StringComparer.OrdinalIgnoreCase);
			foreach (var model in models)
			{
				if (!model.IsGitSourceControlled)
					continue;
				string root = getRepositoryRoot(model.SolutionFolder);
				if (string.IsNullOrEmpty(root))
					continue;
				root = Path.GetFullPath(root);
				if (!groups.TryGetValue(root, out var list))
					groups[root] = list = new List<ISolutionProjectModel>();
				list.Add(model);
			}
			return groups;
		}

		private static async Task RunGitAsync(string repoDir, string args, CancellationToken ct)
		{
			var result = await ProcessRunner.RunAsync("git", $"-C \"{repoDir}\" {args}", cancellationToken: ct).ConfigureAwait(false);
			if (!result.Success)
				throw new InvalidOperationException(
					$"git {args} in \"{repoDir}\" failed with exit code {result.ExitCode}: {result.StdErr.Trim()}");
		}
	}
}
