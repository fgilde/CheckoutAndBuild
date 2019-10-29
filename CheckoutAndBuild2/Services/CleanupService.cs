using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.Build.Evaluation;

namespace FG.CheckoutAndBuild2.Services
{

    public class CleanupService : BaseService, IOperationService
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>		
        public CleanupService(IServiceProvider serviceProvider)
            : base(serviceProvider)
        { }

        public CleanupService()
        { }


        #region IOperationService

        public Guid ServiceId => ServiceIds.CleanServiceId.ToGuid();
        public int Order => ServicePriorities.CleanupServicePriority;
        public bool AllowScriptExport => true;

        public ScriptExportType[] SupportedScriptExportTypes => new[] { ScriptExportType.Batch, ScriptExportType.Powershell };


        public string GetScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, ScriptExportType exportType)
        {
            return exportType == ScriptExportType.Batch ? GetBatchScript(solutionProjects, settings) : GetPowershellScript(solutionProjects, settings);
        }

        private string GetPowershellScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var outputPath in GetOutputPathes(solutionProjects, settings).Distinct())
                builder.AppendLine($" If (Test-Path '{outputPath}')" + "{" + $"Remove-Item '{outputPath}' -Recurse" + "}");
            return builder.ToString();
        }

        private string GetBatchScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var outputPath in GetOutputPathes(solutionProjects, settings).Distinct())
                builder.AppendLine($"rmdir /s /q \"{outputPath}\"");
            return builder.ToString();
        }

        public ImageSource ContextMenuImage => Images.Clean.ToImageSource();

        public string OperationName => "Clean";

        protected override Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken)
        {
            return CleanSolutionsAsync(solutionProjects, settings, cancellationToken);
        }

        private static IEnumerable<string> GetOutputPathes(IEnumerable<ISolutionProjectModel> solutionProjects,
            IServiceSettings settings, Action<ISolutionProjectModel, Project> onProject = null)
        {
            var pathes = GetOutputPathesCore(solutionProjects, settings, onProject).OrderBy(s => s.Length).Distinct().ToList();
            return pathes.Where(s => !pathes.Any(s1 => FileHelper.IsSubPathOf(s, s1)));
        }

        private static IEnumerable<string> GetOutputPathesCore(IEnumerable<ISolutionProjectModel> solutionProjects,
            IServiceSettings settings, Action<ISolutionProjectModel, Project> onProject = null)
        {
            foreach (var solutionProject in solutionProjects)
            {
                var serviceSettings = settings.GetSettingsFromProvider<CleanServiceSettings>(solutionProject);
                foreach (var path in serviceSettings.CustomCleanPathes.Where(s => !string.IsNullOrEmpty(s)))
                {
                    if (File.Exists(path) || Directory.Exists(path))
                        yield return path;
                    else                                            
                        yield return Path.GetFullPath(Path.Combine(solutionProject.SolutionFolder, path));                    
                }

                foreach (var project in solutionProject.GetSolutionProjects())
                {
                    onProject?.Invoke(solutionProject, project);
                    string outputPath = project.GetOutputPath();
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        var directoryName = Path.GetDirectoryName(project.FullPath);
                        if (!string.IsNullOrEmpty(directoryName))
                            yield return Path.GetFullPath(Path.Combine(directoryName, outputPath));
                    }
                }
            }
        }

        #endregion


        public async Task CleanSolutionsAsync(IEnumerable<ISolutionProjectModel> projectViewModels, IServiceSettings settings,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var viewModels = projectViewModels as ProjectViewModel[] ?? projectViewModels.ToArray();
            statusService.InitOrAttach(viewModels.Length, $"Cleaning {viewModels.Length} Solutions");
            try
            {
                await cancellationToken.WaitWhenPaused();
                await Task.WhenAll(viewModels.Select(model => CleanSolutionAsync(model, settings, cancellationToken)));
            }
            catch (TaskCanceledException)
            { }
        }

        public async Task CleanSolutionAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!IsCancelled(projectViewModel, cancellationToken))
            {               
                await cancellationToken.WaitWhenPaused();
                using (new PauseCheckedActionScope(() => projectViewModel.CurrentOperation = Operations.Clean, () => projectViewModel.CurrentOperation = Operations.None, cancellationToken))
                {
                    var externalActionService = serviceProvider.Get<ExternalActionService>();
                    await externalActionService.RunExternalPreActions(projectViewModel, this, cancellationToken: cancellationToken);
                    await Task.Run(async () =>
                    {
                        if (!File.Exists(projectViewModel.ItemPath))
                            return;
                        projectViewModel.ResetProgress();

                        foreach (var outputPath in GetOutputPathes(new[] { projectViewModel }, settings, (model, project) => model.IncrementProgress()))
                        {
                            await cancellationToken.WaitWhenPaused();
                            if (cancellationToken.IsCancellationRequested)
                                return;
                            TryDelete(outputPath);
                        }
                    }, cancellationToken);
                    await externalActionService.RunExternalPostActions(projectViewModel, this, true, cancellationToken: cancellationToken);
                    await cancellationToken.WaitWhenPaused();
                    statusService.IncrementStep();
                }
            }
        }

        private void TryDelete(string outPutDirectory)
        {
            try
            {
                Output.WriteLine("Delete: " + outPutDirectory);
                if (Directory.Exists(outPutDirectory))
                {
                    Directory.Delete(outPutDirectory, true);
                }
                else if (File.Exists(outPutDirectory))
                {
                    File.Delete(outPutDirectory);
                }
            }
            catch (Exception e)
            {
                Output.WriteLine(e.Message);
            }
        }
    }
}