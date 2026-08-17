using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Settings;

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

                var serviceProjects = context.ServiceProjectFilter == null
                    ? includedProjects
                    : includedProjects.Where(p => context.ServiceProjectFilter(service, p)).ToList();
                if (serviceProjects.Count == 0)
                    continue;

                await RunServiceScriptsAsync(context, service, serviceProjects, isPre: true, i, orderedServices.Count, cancellation).ConfigureAwait(false);
                RunCustomActions(context, service, serviceProjects, isPre: true, i, orderedServices.Count);

                try
                {
                    await service.ExecuteAsync(serviceProjects, context.Settings, cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    ReportError(context, service.OperationName, i, orderedServices.Count,
                        $"Error in {service.OperationName}-Service: {e.Message}");
                }

                RunCustomActions(context, service, serviceProjects, isPre: false, i, orderedServices.Count);
                await RunServiceScriptsAsync(context, service, serviceProjects, isPre: false, i, orderedServices.Count, cancellation).ConfigureAwait(false);

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

        private static void RunCustomActions(PipelineContext context, IOperationService service,
            IReadOnlyList<ISolutionProjectModel> projects, bool isPre, int index, int count)
        {
            if (context.CustomActions == null)
                return;

            foreach (var action in context.CustomActions)
            {
                foreach (var project in projects)
                {
                    try
                    {
                        if (isPre)
                            action.RunPreAction(service, project, context.Settings);
                        else
                            action.RunPostAction(service, project, null, context.Settings);
                    }
                    catch (Exception e)
                    {
                        ReportError(context, service.OperationName, index, count,
                            $"Custom action {action.GetType().Name} ({(isPre ? "pre" : "post")} {service.OperationName}) failed: {e.Message}");
                    }
                }
            }
        }

        private static async Task RunServiceScriptsAsync(PipelineContext context, IOperationService service,
            IReadOnlyList<ISolutionProjectModel> projects, bool isPre, int index, int count,
            PausableCancellationTokenSource cancellation)
        {
            if (context.Settings == null)
                return;

            foreach (var project in projects)
            {
                if (cancellation.IsCancellationRequested)
                    return;

                string script;
                try
                {
                    var misc = context.Settings.GetSettingsFromProvider<MiscellaneousSettings>(project);
                    script = isPre ? misc.PreServiceScriptFile : misc.PostServiceScriptFile;
                }
                catch (Exception)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(script) || !File.Exists(script))
                    continue;

                var result = await RunScriptAsync(script, cancellation,
                    $"\"{service.OperationName}\" \"{project.ItemPath}\"").ConfigureAwait(false);
                if (result.ExitCode != 0)
                    ReportError(context, service.OperationName, index, count,
                        $"{(isPre ? "Pre" : "Post")}-service script '{script}' failed for {project.SolutionFileName} with exit code {result.ExitCode}. {result.StdErr}".TrimEnd());
            }
        }

        private static Task<ProcessResult> RunScriptAsync(string path, PausableCancellationTokenSource cancellation, string scriptArgs = null)
        {
            var workingDir = Path.GetDirectoryName(path);
            string suffix = string.IsNullOrEmpty(scriptArgs) ? "" : " " + scriptArgs;
            return path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? ProcessRunner.RunAsync("powershell", $"-ExecutionPolicy Bypass -File \"{path}\"{suffix}", workingDir, cancellationToken: cancellation.Token)
                : ProcessRunner.RunAsync("cmd", $"/s /c \"\"{path}\"{suffix}\"", workingDir, cancellationToken: cancellation.Token);
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
