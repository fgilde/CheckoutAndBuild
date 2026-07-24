using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Controls;
using Task = System.Threading.Tasks.Task;

namespace FG.CheckoutAndBuild2.Services
{

    public class LocalBuildService : BaseService, IOperationService
    {
        private string msBuildExe = string.Empty;
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>		
        public LocalBuildService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        { }

        public LocalBuildService()
        { }

        #region IOperationService

        public Guid ServiceId => ServiceIds.BuildServiceId.ToGuid();

        public string OperationName => "Build";

        public int Order => ServicePriorities.BuildServicePriority;

        public bool AllowScriptExport => true;

        public ImageSource ContextMenuImage => Images.BuildSolution_104.ToImageSource();

        public ScriptExportType[] SupportedScriptExportTypes => new[] { ScriptExportType.Batch, ScriptExportType.Powershell };

        public string GetScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, ScriptExportType exportType)
        {
            if (exportType == ScriptExportType.Batch)            
                return string.Join(Environment.NewLine, solutionProjects.Select(model => GetBatchScript(model, settings)));
            return GetPowershellScript(solutionProjects, settings);
        }

        private string GetPowershellScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings)
        {
            CheckMSBuildExe();
            var builder = new StringBuilder().AppendLine($"set-Alias MSBUILD \"{msBuildExe}\" ");
            foreach (ISolutionProjectModel solutionProject in solutionProjects)
            {
                var targetsArgs = string.Join(",", GetBuildTargets(solutionProject));
                var properties = GetBuildProperties(solutionProject, settings);
                string proStr = string.Empty;
                var buildSettings = settings.GetSettingsFromProvider<BuildServiceSettings>(solutionProject);
                string solutionToBuild = !solutionProject.IsDelphiProject && buildSettings.BuildMode == BuildMode.MergedBuild ? solutionProject.ParentWorkingFolder.LocalItem + "\\Build.sln" : solutionProject.ItemPath;
                if(properties.Any())
                    proStr = " /property:" + string.Join(" /property:", properties.Select(pair => $"{pair.Key}={pair.Value}"));
                builder.AppendLine($"MSBUILD \"{solutionToBuild}\" /m /target:'{targetsArgs}'{proStr}");
            }
            return builder.ToString();
        }

        private string GetBatchScript(ISolutionProjectModel solutionProject, IServiceSettings settings)
        {
            CheckMSBuildExe();            
            StringBuilder builder = new StringBuilder();
            var targetsArgs = string.Join(",", GetBuildTargets(solutionProject));
            var properties = GetBuildProperties(solutionProject, settings);
            foreach (var property in properties)            
                builder.AppendLine($"SET {property.Key}={property.Value}");            
            if (solutionProject.IsDelphiProject)            
                builder.AppendLine(string.Format("\"{0}\" \"{1}\" /p:Config={3};Platform={4} /t:{2}", msBuildExe, solutionProject.ItemPath, targetsArgs, properties["CONFIG"], properties["PLATFORM"]));            
            else                      
                builder.AppendLine(string.Format("\"{0}\" /target:{2} /verbosity:q \"{1}\"", msBuildExe, settings.GetSettingsFromProvider<BuildServiceSettings>(solutionProject).BuildMode == BuildMode.MergedBuild ? solutionProject.ParentWorkingFolder.LocalItem + "\\Build.sln" : solutionProject.ItemPath, targetsArgs));
            
            return builder.ToString();
            
        }

        private void CheckMSBuildExe()
        {
            if (!File.Exists(msBuildExe))
                msBuildExe = new BuildParameters().NodeExeLocation;
        }

        protected override Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken)
        {
            return BuildSolutionsAsync(solutionProjects, settings, cancellationToken);
        }

        #endregion

        private async Task<Tuple<List<ProjectViewModel>, List<ProjectViewModel>>> MergeContingentAsync(ISolutionProjectModel[] projects, IServiceSettings settings, CancellationToken cancellationToken)
        {
            try
            {
                var projectsToMerge = projects.Where(model => !model.IsDelphiProject && settings.GetSettingsFromProvider<BuildServiceSettings>(model).BuildMode == BuildMode.MergedBuild).Cast<ProjectViewModel>().ToList();
                if (projectsToMerge.Any())
                {
                    var packers = await StaticCommands.MergeSolutionsAsync(projectsToMerge, cancellationToken: cancellationToken);
                    var newSolutions = new List<ProjectViewModel>();                
                    foreach (var packer in packers.Where(packer => packer != null))
                    {
                        var projectsForThisPacker = projects.Where(model => packer.InputSolutions.Contains(model.ItemPath)).ToList();
                        ISolutionProjectModel m = projectsForThisPacker.FirstOrDefault(); 
                        if (m is ProjectViewModel)
                        {
                            var oldProjectModel = (ProjectViewModel)m;
                            var newMergedBuildSolution = oldProjectModel.ParentWorkingFolder.AddProject(packer.OutputSolutionFileName, true, true);

                            // TODO: doof  
                            if (settings.BuildProperties == null || !settings.BuildProperties.Any() ||
                                settings.BuildProperties.Keys.Select(s => s.ToLower()).Contains("platform"))
                            {
                                newMergedBuildSolution.BuildProperties.Add("Platform", "Any CPU");    
                            }
                        
                            newMergedBuildSolution.BuildPriority = projectsForThisPacker.OrderBy(model => model.BuildPriority).Last().BuildPriority;
                            newSolutions.Add(newMergedBuildSolution);
                        }
                    }

                    // OperationStatus der neuen Solution auf alle anderen projizieren
                    newSolutions.Apply(model => model?.OnChange(() => model.CurrentOperation, o => projectsToMerge.SetOperations(o)));
                    var mergedResult = Tuple.Create(newSolutions, projectsToMerge);
                    // Cancel action setzen
                    cancellationToken.Register(() =>
                    {
                        mergedResult.Item1.SetOperations(Operations.None);
                        mergedResult.Item2.SetOperations(Operations.None);
                    });
                    return mergedResult;
                }
            }
            catch (Exception e)
            {
                Output.Exception(e);
            }
            return null;
        }


        public async Task BuildSolutionsAsync(IEnumerable<ISolutionProjectModel> projectViewModels, IServiceSettings settings,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            bool delphiCancelRegistered = false, allDone = false;
            IDictionary<string, string> envVars = null;
            var projects = projectViewModels.ToList();
            await cancellationToken.WaitWhenPaused();
            #region Merge Solutions perhaps

            var mergedResult = await MergeContingentAsync(projects.ToArray(), settings, cancellationToken);
            if (mergedResult != null && mergedResult.Item1.Any()) // Something is merged
            {                                   
                projects.RemoveRange(mergedResult.Item2);                        
                await serviceProvider.Get<NugetRestoreService>().ExecuteAsync(mergedResult.Item1, settings, cancellationToken);
                projects.AddRange(mergedResult.Item1);
            }

            #endregion

            if (!IsCancelled(projects, cancellationToken))
            {
                try
                {
                    statusService.InitOrAttach(projects.Count, $"Building {projects.Count} Solutions");
                    var externalActionService = serviceProvider.Get<ExternalActionService>();

                    await cancellationToken.WaitWhenPaused();
                    projects.SetOperations(Operations.Queued);
                    
                    envVars = await RegisterBDSRequiredEnvVarsAsync(projects.Any(model => model.IsDelphiProject), settings);

                    Output.ClearTasks(!serviceProvider.Get<MainLogic>().IsCompleteRunning);

                    var buildManager = new BuildManager("CheckoutAndBuildBuilder");

                    cancellationToken.Register(() => { if (!allDone) buildManager.CancelAllSubmissions(); });

                    var logger = new TraceLogger(settings.LogLevel, cancellationToken);
                    BuildParameters buildParameters = GetBuildParameters(logger);

                    buildManager.BeginBuild(buildParameters);

                    foreach (IGrouping<int, ISolutionProjectModel> g in from m in projects orderby m.BuildPriority group m by m.BuildPriority)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var submissions = new Dictionary<BuildSubmission, ISolutionProjectModel>();
                        var buildResults = new List<Task<BuildResult>>();
                        await cancellationToken.WaitWhenPaused();
                        foreach (ISolutionProjectModel solution in g.Where(model => !IsCancelled(model)))
                        {
                            await cancellationToken.WaitWhenPaused();
                            if (cancellationToken.IsCancellationRequested) break;
                            var properties = GetBuildProperties(solution, settings);
                            var buildRequestData = new BuildRequestData(solution.ItemPath, properties, null, GetBuildTargets(solution), buildParameters.HostServices);
                            submissions.Add(buildManager.PendBuildRequest(buildRequestData), solution);
                        }

                        using (TrackProgress(submissions, logger))
                        {
                            foreach (KeyValuePair<BuildSubmission, ISolutionProjectModel> pair in submissions.Where(pair => !IsCancelled(pair.Value)))
                            {
                                var project = pair.Value;                                
                                var buildServiceSettings = settings.GetSettingsFromProvider<BuildServiceSettings>(project);
                                await cancellationToken.WaitWhenPaused();
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    Output.WriteLine("Cancelled Build for " + project.ItemPath);
                                    project.CurrentOperation = Operations.None;
                                    break;
                                }

                                await cancellationToken.WaitWhenPaused();
                                project.CurrentOperation = !project.IsDelphiProject ? Operations.Build : Operations.BuildIndeterminate;

                                await externalActionService.RunExternalPreActions(project, this, cancellationToken: cancellationToken);

                                if (buildServiceSettings.KillDependendProcesses)
                                    await project.KillRunningInstancesAsync(true, cancellationToken);
                                if (project.IsDelphiProject && !delphiCancelRegistered)
                                {
                                    delphiCancelRegistered = true;
                                    cancellationToken.Register(KillDelphiCompiler);
                                }
                                TaskCompletionSource<BuildResult> tcs = new TaskCompletionSource<BuildResult>();
                                if (!IsCancelled(pair.Value)) { 
                                    pair.Key.ExecuteAsync(buildSubmission => SetTaskCompletionResult(buildSubmission, tcs), null);
                                    await cancellationToken.WaitWhenPaused();
                                    var task = tcs.Task.ContinueWith(async t =>
                                    {
                                        var buildResult = t.Result;
                                        await externalActionService.RunExternalPostActions(project, this, t.Result, settings, cancellationToken);
                                        await cancellationToken.WaitWhenPaused();
                                        statusService.IncrementStep();
                                        Output.WriteLine("Finished Build for " + project.ItemPath + " " + buildResult.OverallResult);
                                        project.CurrentOperation = Operations.None;

                                        if (buildResult.OverallResult == BuildResultCode.Failure)
                                        {
                                            project.SetResult(new ValidationResult(false, GetErrorsViewModel(project, buildResult)));
                                            if (buildServiceSettings.CancelQueuedOnFailures)
                                                serviceProvider.Get<MainViewModel>().CompleteCancelCommand.Execute(null);
                                        }
                                        else
                                            project.SetResult(ValidationResult.ValidResult);

                                        return buildResult;
                                    }, cancellationToken, TaskContinuationOptions.NotOnCanceled, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
                                    buildResults.Add(task);
                                }
                            }
                            await Check.TryCatchAsync<TaskCanceledException>(Task.WhenAll(buildResults));
                        }
                    }
                    allDone = true;
                    if (!cancellationToken.IsCancellationRequested)
                        await buildManager.EndBuildAsync(cancellationToken);

                }
                catch (Exception e)
                {
                    Output.Exception(e);
                }
                finally
                {
                    #region Finalize

                    #pragma warning disable 4014 // Result doesn't matter
                    EnvironmentHelper.RemoveVariablesAsync(envVars);
                    #pragma warning restore 4014
                    await cancellationToken.WaitWhenPaused();
                    projects.Where(model => model.ErrorContent == null).Apply(model => model.SetDefaultImageValues());                    
                    projects.SetOperations(Operations.None);
                    mergedResult?.Item2.SetOperations(Operations.None);
                    mergedResult?.Item1.Apply(model =>
                    {
                        model.ParentWorkingFolder.RemoveCustomProject(model, true);
                        if (model.Result == ValidationResult.ValidResult)
                            File.Delete(model.ItemPath);
                        else
                            Output.ActionLink($"Build Failed: delete Temporary Build Solution {model.SolutionFileName}?", () => File.Delete(model.ItemPath), NotificationType.Error, $"Click here to delete temporary merged Build Solution {model.SolutionFileName}");
                    });                 

                    #endregion             
                 }
            }
        }

        public IDictionary<string, string> GetBuildProperties(ISolutionProjectModel projectViewModel, IServiceSettings settings)
        {
            IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var p in settings.BuildProperties)
            {
                if (!globalProperties.ContainsKey(p.Key))
                    globalProperties.Add(p.Key, p.Value);
            }

            if (projectViewModel.IsDelphiProject)
                globalProperties = globalProperties.MergeWith(GetDelphiEnvironments(settings, projectViewModel));
            var res = globalProperties.MergeWith(GetProjectSpecificBuildProperties(projectViewModel, settings));
            return EnsureNotNull(res);   
        }


        private static IDictionary<string, string> EnsureNotNull(IDictionary<string, string> res)
        {
            foreach (var re in res.ToList())
            {
                if (re.Value == null)
                    res[re.Key] = string.Empty;
            }
            return res;
        }

        private void SetTaskCompletionResult(BuildSubmission buildSubmission, TaskCompletionSource<BuildResult> tcs)
        {
            if (buildSubmission.BuildResult.Exception != null)
            {
                if (buildSubmission.BuildResult.Exception is BuildAbortedException)
                    tcs.SetCanceled();
                else
                    tcs.TrySetException(buildSubmission.BuildResult.Exception);
            }
            else
                tcs.SetResult(buildSubmission.BuildResult);
        }

        private void KillDelphiCompiler()
        {
            foreach (var process in Process.GetProcesses())
            {
                if (process.ProcessName == "dcc" || process.ProcessName == "dcc64" || process.ProcessName == "dcc32")
                    process.Kill();
            }
        }

        private BuildParameters GetBuildParameters(TraceLogger logger)
        {
            var settings = settingsService.GetSettingsFromProvider<BuildServiceSettings>();
            var maxNodeCount = settings.MaxNodeCount > 0 ? settings.MaxNodeCount : Environment.ProcessorCount;
            var buildParameters = new BuildParameters
            {
                HostServices = new HostServices(),
                BuildThreadPriority = settings.ThreadPriority,
                Loggers = new[] { logger },
                EnableNodeReuse = settings.EnableNodeReuse,
                MaxNodeCount = maxNodeCount
            };
            return buildParameters;
        }

        private object GetErrorsViewModel(ISolutionProjectModel project, BuildResult buildResult)
        {
            var res = new BuildErrorsViewModel(Output.FindErrorTasks(project), this, project, serviceProvider);
            if (buildResult.Exception != null)
                res.Message = buildResult.Exception.Message;
            return res;
        }

        private IDisposable TrackProgress(Dictionary<BuildSubmission, ISolutionProjectModel> submissions, TraceLogger logger)
        {
            foreach (var pair in submissions)
                pair.Value.ResetProgress();

            EventHandler<BuildEventContext> handler = (sender, args) =>
            {
                var pair = submissions.FirstOrDefault(s => s.Key.SubmissionId == args.SubmissionId);
                pair.Value.IncrementProgress();
            };
            logger.ProjectFinished += handler;

            return new DisposableCookie(() =>
            {
                logger.ProjectFinished -= handler;
            });
        }

        private string GetDelphiBinPath(IServiceSettings settings)
        {
            var delphiFileInfo = new FileInfo(settings.DelphiPath);
            if (delphiFileInfo.Exists && delphiFileInfo.Directory?.Parent != null)
                return delphiFileInfo.Directory.Parent.FullName;
            return null;
        }

        private IDictionary<string, string> GetDelphiEnvironments(IServiceSettings settings, ISolutionProjectModel projectViewModel)
        {
            string delphiDir = GetDelphiBinPath(settings);
            if (delphiDir != null && Directory.Exists(delphiDir))
            {
                var buildProps = settings.BuildProperties;
                var platform = FindPlatform(buildProps,projectViewModel);
                var versionInfo = FileVersionInfo.GetVersionInfo(settings.DelphiPath);
                string version = GetCrazyDelphiEnvVersionShit(versionInfo);

                string envOptionDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Embarcadero", "BDS", version);
                if (!Directory.Exists(envOptionDir))
                    Directory.CreateDirectory(envOptionDir);
                var targetEnvFileName = Path.Combine(envOptionDir, "EnvOptions.proj");

                //if (!File.Exists(targetEnvFileName))
                File.WriteAllBytes(targetEnvFileName, FindEnvOptions(version));

                var bdsCommon = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "RAD Studio", version);
                var res = new Dictionary<string, string>
                {
                    {"PLATFORM", platform},
                    {"CONFIG", @"Debug"},
                    {"DELPHIPATH", delphiDir},
                    {"BDS", @"%DELPHIPATH%"},
                    {"BDSCOMMONDIR", bdsCommon},
                    {"FrameworkDir", @"C:\Windows\Microsoft.NET\Framework\v3.5"},
                    {"FrameworkVersion", @"v3.5"},
                    {"FrameworkSDKDir", @""},
                    {"PATH", @"%FrameworkDir%;%FrameworkSDKDir%;%DELPHIPATH%\bin;%DELPHIPATH%\bin64;"},
                    {"BDSBIN", @"%BDS%\bin"},
                    {"BDSINCLUDE", @"%BDS%\include"},
                    {"BDSLIB", @"%BDS%\lib"},
                    {"LANGDIR", @"DE"}
                };
                return res.ResolvePlaceHolders();
            }
            return new Dictionary<string, string>();
        }

        private static byte[] FindEnvOptions(string version)
        {
            version = version.Replace(".0", string.Empty);
            ResourceManager manager = new ResourceManager(typeof(Resources));
            var res = manager.GetObject($"EnvOptions_{version}") as byte[];
            if (res != null)
                return res;
            return Resources.EnvOptions;
        }

        private string GetCrazyDelphiEnvVersionShit(FileVersionInfo productVersion)
        {
            if (productVersion.FileMajorPart == 16)
                return "9.0"; // Die bei delphi sind halt irgendwie dumm 
            return (productVersion.FileMajorPart - 6) + ".0";
        }

        private async Task<IDictionary<string, string>> RegisterBDSRequiredEnvVarsAsync(bool condition, IServiceSettings settings)
        {
            IDictionary<string, string> res = new Dictionary<string, string>();
            if (condition)
            {
                await EnvironmentHelper.SetEnvironmentVariableWithValueReplaceAsync("BDS", GetDelphiBinPath(settings));
            }
            return res;
        }

        private IDictionary<string, string> GetProjectSpecificBuildProperties(ISolutionProjectModel projectViewModel, IServiceSettings settings)
        {
            var res = serviceProvider.Get<ExternalActionService>().GetDefaultBuildProperties(projectViewModel, settings) ?? new Dictionary<string, string>();
            var buildProperties = projectViewModel.BuildProperties;
            if (buildProperties != null && buildProperties.Any())
                return res.MergeWith(buildProperties);
            return res;
        }

        private string[] GetBuildTargets(ISolutionProjectModel projectViewModel)
        {
            var buildTargets = projectViewModel.BuildTargets ?? new List<string>();
            var res = buildTargets.Where(s => !string.IsNullOrEmpty(s)).ToArray();
            if (res.Length == 0)
                res = new[] { "Build" };
            return res;
        }

        private static string FindPlatform(IDictionary<string, string> buildProps, ISolutionProjectModel projectViewModel)
        {
            string platform;
            if (buildProps.ContainsKey("Platform"))
            {
                if (buildProps["Platform"] == "x64")
                    platform = "Win64";
                else if (buildProps["Platform"] == "x86")
                    platform = "Win32";
                else
                    platform = buildProps["Platform"];
            }
            else
            {
                var project = projectViewModel.GetSolutionProjects().FirstOrDefault();
                platform = project?.GetPropertyValue("Platform");
            }
            return platform ?? (Environment.Is64BitOperatingSystem ? "Win64" : "Win32");
        }

        /// <summary>
        /// Default settings
        /// </summary>
        /// <returns></returns>
	    internal static List<BindablePair<string, string>> GetDefaultBuildProperties()
        {
            return new List<BindablePair<string, string>>
            {
                new BindablePair<string, string>("BuildInParallel", "true"),
                new BindablePair<string, string>("RunCodeAnalysis", "false")
            };
        }
    }
}