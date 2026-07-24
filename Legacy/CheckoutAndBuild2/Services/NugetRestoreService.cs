using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.Build.Execution;

namespace FG.CheckoutAndBuild2.Services
{
    public class NugetRestoreService : BaseService, IOperationService
    {

        public NugetRestoreService(IServiceProvider serviceProvider) : base(serviceProvider)
		{}

        public NugetRestoreService()
        {}
        
        public string OperationName => "Nuget Restore";
        public Guid ServiceId => ServiceIds.NugetRestoreServiceId.ToGuid();
        public int Order => ServicePriorities.NugetRestoreServicePriority;

        #region Generate Script

        public bool AllowScriptExport => true;

        public ScriptExportType[] SupportedScriptExportTypes => new[] { ScriptExportType.Batch, ScriptExportType.Powershell };

        public string GetScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, ScriptExportType exportType)
        {
            if (settings.GetSettingsFromProvider<BuildServiceSettings>().BuildMode == BuildMode.MergedBuild)
            {
                if (exportType == ScriptExportType.Powershell)
                    return $"set-alias NUGET \"{NugetExe(solutionProjects.First(), settings)}\"{Environment.NewLine}Invoke-Expression \" & 'NUGET' restore '{solutionProjects.First().ParentWorkingFolder.LocalItem}\\Build.sln'\"";
                return $"\"{NugetExe(solutionProjects.First(), settings)}\" restore \"{solutionProjects.First().ParentWorkingFolder.LocalItem}\\Build.sln\"";
            }

            return string.Join(Environment.NewLine, solutionProjects.Select(model => exportType == ScriptExportType.Batch ? GetBatchScript(model, settings) : GetPowershellScript(model, settings)));
        }

        private string GetPowershellScript(ISolutionProjectModel model, IServiceSettings settings)
        {
            NugetServiceSettings nugetServiceSettings = settings.GetSettingsFromProvider<NugetServiceSettings>(model);            
            return new StringBuilder()
                .AppendLine($"set-alias NUGET \"{NugetExe(model, settings)}\"")
                .AppendLineWhen($"& NUGET restore \"{model.ItemPath}\"", s => nugetServiceSettings.NugetAction == NugetAction.Restore || nugetServiceSettings.NugetAction == NugetAction.InstallAndRestore)
                .AppendLinesWhen(Directory.EnumerateFiles(model.SolutionFolder, "packages.config", SearchOption.AllDirectories)
                    .Select(s => new FileInfo(s))
                    .Select(file => $"& NUGET install \"{file.FullName}\" -OutputDirectory \"{file.Directory.Parent.FullName}\\packages\""), s => nugetServiceSettings.NugetAction == NugetAction.Install || nugetServiceSettings.NugetAction == NugetAction.InstallAndRestore).ToString();
        }

        private string GetBatchScript(ISolutionProjectModel model, IServiceSettings settings)
        {
            NugetServiceSettings nugetServiceSettings = settings.GetSettingsFromProvider<NugetServiceSettings>(model);
            return new StringBuilder()
                .AppendLineWhen($"\"{NugetExe(model, settings)}\" restore -NonInteractive {AddMsBuildPath()} \"{model.ItemPath}\"", s => nugetServiceSettings.NugetAction == NugetAction.Restore || nugetServiceSettings.NugetAction == NugetAction.InstallAndRestore)
                .AppendLinesWhen(Directory.EnumerateFiles(model.SolutionFolder, "packages.config", SearchOption.AllDirectories)
                    .Select(s => new FileInfo(s))
                    .Select(file => $"\"{NugetExe(model, settings)}\" install -NonInteractive \"{file.FullName}\" -OutputDirectory \"{file.Directory.Parent.FullName}\\packages\""), s => nugetServiceSettings.NugetAction == NugetAction.Install || nugetServiceSettings.NugetAction == NugetAction.InstallAndRestore).ToString();
        }

        #endregion

        public ImageSource ContextMenuImage => Images.package_32xLG.ToImageSource();

        public string NugetExe(ISolutionProjectModel solution, IServiceSettings settings)
        {
            var nugetPath = settings.GetSettingsFromProvider<NugetServiceSettings>(solution).NugetExeLocation;
            var result = !string.IsNullOrEmpty(nugetPath) && File.Exists(nugetPath) ? nugetPath : Path.Combine(solution.SolutionFolder, ".nuget", "nuget.exe");
            if (!File.Exists(result))
                result = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetCallingAssembly().Location), "Resources", "nuget481.exe"); //"nuget35.exe" //"nuget28.exe"
            return result;
        }

        protected override async Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> projectViewModels, IServiceSettings settings, CancellationToken cancellationToken)
        {
            var viewModels = projectViewModels as ISolutionProjectModel[] ?? projectViewModels.ToArray();
            statusService.InitOrAttach(viewModels.Length, "Nuget Restore");
            await cancellationToken.WaitWhenPaused();
            cancellationToken.Register(KillNuget);
            if (settings.GetSettingsFromProvider<NugetServiceSettings>().RunParallel)
                await RestoreParallelAsync(viewModels, settings, cancellationToken);
            else
                await RestoreSerializedAsync(viewModels, settings, cancellationToken);
        }

        private async Task RestoreParallelAsync(ISolutionProjectModel[] viewModels, IServiceSettings settings, CancellationToken cancellationToken)
        {
            await Task.WhenAll(viewModels.Select(model => RestorePackagesForSolutionAsync(model, settings, cancellationToken)));
        }

        private async Task RestoreSerializedAsync(ISolutionProjectModel[] viewModels, IServiceSettings settings, CancellationToken cancellationToken)
        {
            foreach (var solutionProjectModel in viewModels.OrderBy(solution => solution.BuildPriority))
            {
                await RestorePackagesForSolutionAsync(solutionProjectModel, settings, cancellationToken);
            }
        }

        private void KillNuget()
        {
            foreach (var process in Process.GetProcesses())
            {                
                // TODO: nur killn wenn selbst gestartet
                if (process.ProcessName == "nuget481" || process.ProcessName == "nuget")
                    Check.TryCatch<Exception>(()=> process.Kill());
            }            
        }

        private async Task RestorePackagesForSolutionAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings, CancellationToken cancellationToken)
        {
            if (!IsCancelled(projectViewModel, cancellationToken))
            {
                NugetServiceSettings nugetServiceSettings = settings.GetSettingsFromProvider<NugetServiceSettings>(projectViewModel);
                using (new PauseCheckedActionScope(() => projectViewModel.CurrentOperation = Operations.NugetRestore, () => projectViewModel.CurrentOperation = Operations.None, cancellationToken))
                {
                    var externalActionService = serviceProvider.Get<ExternalActionService>();
                    await externalActionService.RunExternalPreActions(projectViewModel, this, cancellationToken: cancellationToken);
                    if (nugetServiceSettings.NugetAction == NugetAction.Restore || nugetServiceSettings.NugetAction == NugetAction.InstallAndRestore)
                        await NugetRestoreAsync(projectViewModel, settings, cancellationToken);
                    if (nugetServiceSettings.NugetAction == NugetAction.Install || nugetServiceSettings.NugetAction == NugetAction.InstallAndRestore)
                        await NugetInstallAsync(projectViewModel, settings, cancellationToken);
                    if (nugetServiceSettings.NugetAction == NugetAction.Reinstall)
                        await NugetReinstallAsync(projectViewModel, settings, cancellationToken);
                    await externalActionService.RunExternalPostActions(projectViewModel, this, true, cancellationToken: cancellationToken);
                    statusService.IncrementStep();
                }
            }
        }
        private string msBuildPath = "";

        private void CheckMSBuildPath()
        {
            if (!Directory.Exists(msBuildPath))
                msBuildPath = new FileInfo(new BuildParameters().NodeExeLocation).Directory?.FullName;
        }
        private async Task NugetInstallAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings, CancellationToken cancellationToken)
        {
            await Task.WhenAll(Directory.EnumerateFiles(projectViewModel.SolutionFolder, "packages.config", SearchOption.AllDirectories)
                .Select(s => new FileInfo(s))
                .Select(file => ScriptHelper.ExecuteScriptAsync(NugetExe(projectViewModel, settings), $"install -NonInteractive \"{file.FullName}\" -OutputDirectory \"{file.Directory.Parent.FullName}\\packages\"", ScriptExecutionSettings.Default, cancellationToken: cancellationToken)));
        }

        private async Task NugetRestoreAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings,
            CancellationToken cancellationToken)
        {
            CheckMSBuildPath();
            await ScriptHelper.ExecuteScriptAsync(NugetExe(projectViewModel, settings), $"restore -NonInteractive {AddMsBuildPath()} \"{projectViewModel.ItemPath}\"", ScriptExecutionSettings.Default, cancellationToken: cancellationToken);
        }

        private string AddMsBuildPath()
        {
            return $"-MSBuildPath \"{msBuildPath}\"";
        }

        private async Task NugetReinstallAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings,
            CancellationToken cancellationToken)
        {
            var builder = new StringBuilder()
                .AppendLine($"Set-Location -Path \"{projectViewModel.SolutionFolder}\" ")
                .AppendLine("Update-Package -Reinstall");
            await ScriptHelper.ExecutePowershellScriptAsync(builder.ToString(), null, cancellationToken);            
        }
    }
}