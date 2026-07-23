using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Execution;

namespace CheckoutAndBuild.Core.Pipeline
{
    /// <summary>
    /// Orchestrates the operation pipeline (port of MainLogic.RunCheckoutAndBuild):
    /// services sequentially by <see cref="IOperationService.Order"/>, only included projects
    /// sorted by BuildPriority, pre-build script first, post-build script right after the build service.
    /// </summary>
    public sealed class PipelineRunner
    {
        public async Task RunAsync(IReadOnlyList<ISolutionProjectModel> projects,
                                   IEnumerable<IOperationService> services,
                                   PipelineContext context,
                                   PausableCancellationTokenSource cancellation)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (cancellation == null) throw new ArgumentNullException(nameof(cancellation));
            context = context ?? new PipelineContext();

            var includedProjects = projects.Where(p => p.IsIncluded).OrderBy(p => p.BuildPriority).ToList();
            var orderedServices = services.OrderBy(s => s.Order).ToList();
            var buildServiceId = new Guid(ServiceIds.BuildServiceId);

            if (!string.IsNullOrEmpty(context.PreBuildScript))
            {
                var result = await RunScriptAsync(context.PreBuildScript, cancellation).ConfigureAwait(false);
                if (result.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"Pre-build script '{context.PreBuildScript}' failed with exit code {result.ExitCode}. {result.StdErr}".TrimEnd());
            }

            for (int i = 0; i < orderedServices.Count; i++)
            {
                var service = orderedServices[i];
                await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);
                context.Progress?.Report(new PipelineProgress
                {
                    OperationName = service.OperationName,
                    ServiceIndex = i,
                    ServiceCount = orderedServices.Count
                });

                try
                {
                    await service.ExecuteAsync(includedProjects, context.Settings, cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    // old semantics: a failing service is logged and the pipeline continues
                    ReportError(context, service.OperationName, i, orderedServices.Count,
                        $"Error in {service.OperationName}-Service: {e.Message}");
                }

                if (service.ServiceId == buildServiceId && !string.IsNullOrEmpty(context.PostBuildScript)
                    && !cancellation.IsCancellationRequested)
                {
                    var result = await RunScriptAsync(context.PostBuildScript, cancellation).ConfigureAwait(false);
                    if (result.ExitCode != 0)
                        ReportError(context, service.OperationName, i, orderedServices.Count,
                            $"Post-build script '{context.PostBuildScript}' failed with exit code {result.ExitCode}. {result.StdErr}".TrimEnd());
                }
            }
        }

        private static Task<ProcessResult> RunScriptAsync(string path, PausableCancellationTokenSource cancellation)
        {
            var workingDir = Path.GetDirectoryName(path);
            return path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? ProcessRunner.RunAsync("powershell", $"-ExecutionPolicy Bypass -File \"{path}\"", workingDir, cancellationToken: cancellation.Token)
                : ProcessRunner.RunAsync("cmd", $"/s /c \"\"{path}\"\"", workingDir, cancellationToken: cancellation.Token);
        }

        private static void ReportError(PipelineContext context, string operationName, int index, int count, string error)
        {
            context.Progress?.Report(new PipelineProgress
            {
                OperationName = operationName,
                ServiceIndex = index,
                ServiceCount = count,
                Error = error
            });
        }
    }
}
