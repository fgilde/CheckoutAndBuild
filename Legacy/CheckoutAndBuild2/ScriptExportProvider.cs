using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.Win32;

namespace FG.CheckoutAndBuild2
{
    public class ScriptExportProvider
    {
        private readonly IServiceProvider serviceProvider;
        private readonly MainLogic mainLogic;
        internal static bool IsTeamFoundationPowerShellSnapInInstalled => TestPowershellSnapIn();


        public ScriptExportProvider(IServiceProvider serviceProvider)
        {            
            this.serviceProvider = serviceProvider;
            mainLogic = serviceProvider.Get<MainLogic>();
        }

        public static string GetSolutionPackerExe()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), "SolutionPacker.exe");
        }

        public Task<string> GenerateExportScriptAsync(WorkingFolderListViewModel listViewModel, ScriptExportType scriptType,
            CancellationToken cancellationToken = default(CancellationToken))
        {            
            IServiceSettings settings = serviceProvider.Get<SettingsService>().GetMainServiceSettings();
            string powerShellParams = "";//"[string] $source";
            return Task.Run(() =>
            {
                return Check.TryCatch<string, TaskCanceledException>(() =>
                {
                    var builder = new StringBuilder();
                    var servicesToExport = mainLogic.GetIncludedServices().Where(service => service.AllowScriptExport && service.SupportedScriptExportTypes.Contains(scriptType)).OrderBy(service => service.Order);

                    builder.AppendLineIfNotEmpty(GetCallScriptMethodScript(scriptType, settings.PreBuildScriptPath));
                    builder.AppendLineIfNotEmpty(GenerateSolutionPackerMerge(listViewModel.WorkingFolders.SelectMany(model => model.Projects).Where(model => model.IsIncluded).OrderBy(model => model.BuildPriority), settings, scriptType));
                    List<Task<string>> tasks = servicesToExport.Select(service => Task.Run(() =>
                    {
                        var solutionProjectModels = listViewModel.WorkingFolders.SelectMany(model => model.Projects).Where(service.IsIncluded).OrderBy(model => model.BuildPriority);
                        var innerBuilder = new StringBuilder()
                            .AppendLine(GenerateExternalScriptParts(true, service, solutionProjectModels, settings, scriptType))
                            .AppendLine()
                            .AppendLine(CreateHeader(scriptType, service, powerShellParams)).AppendLine();                                                                   
                        innerBuilder.AppendLineIfNotEmpty(service.GetScript(solutionProjectModels, settings, scriptType));
                        innerBuilder.AppendLine(CreateServiceFooter(scriptType));
                        innerBuilder.AppendLine(GenerateExternalScriptParts(false, service, solutionProjectModels, settings, scriptType));
                        return innerBuilder.AppendLine().ToString();
                    }, cancellationToken)).ToList();

                    Check.TryCatch<OperationCanceledException>(() => Task.WhenAll(tasks).ContinueWith(t => builder.AppendLine(string.Join(Environment.NewLine, t.Result)), cancellationToken, TaskContinuationOptions.NotOnCanceled, System.Threading.Tasks.TaskScheduler.Current).Wait(cancellationToken));

                    if (scriptType == ScriptExportType.Powershell)
                        builder.AppendLinesIfNotEmpty(GeneratePowershellMethodCalls(servicesToExport, powerShellParams, settings).ToArray());

                    builder.AppendLineIfNotEmpty(GetCallScriptMethodScript(scriptType, settings.PostBuildScriptPath));

                    var result = ScriptHelper.PrepareScriptVars(builder.AppendLine(scriptType == ScriptExportType.Batch ? "pause;" : "").ToString(), true, scriptType);
                    return new StringBuilder(GetStaticScriptsStarts(scriptType))
                        .AppendLine(result)
                        .AppendLine(GetStaticScriptsEnds(scriptType))
                        .ToString();

                });
            }, cancellationToken);
        }

        private string GenerateExternalScriptParts(bool generatePreCode, IOperationService currentService, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings,
            ScriptExportType scriptExportType )
        {
            var builder = new StringBuilder();
            foreach (var scriptGenerator in CheckoutAndBuild2Package.GetExportedValues<IScriptGenerator>())
            {
                builder.AppendLine(generatePreCode
                    ? scriptGenerator.GeneratePreScriptCode(currentService, solutions, settings, scriptExportType)
                    : scriptGenerator.GeneratePostScriptCode(currentService, solutions, settings, scriptExportType));
            }
            return builder.ToString();
        }

        private string GenerateSolutionPackerMerge(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings,
        ScriptExportType scriptExportType)
        {
            if (settings.GetSettingsFromProvider<BuildServiceSettings>().BuildMode == BuildMode.MergedBuild)
            {
                var builder = new StringBuilder();
                var branchPath = solutionProjects.First().ParentWorkingFolder.LocalItem;
                var solutionStr = string.Join(";", solutionProjects.Select(model => model.SolutionFileName));
                if (scriptExportType == ScriptExportType.Powershell)
                {
                    builder.AppendLine("function MergeSolutions()");
                    builder.AppendLine("{");
                    builder.AppendLine($"$solutionPacker = \"{GetSolutionPackerExe()}\"");
                    builder.AppendLine($"$source = \"{branchPath}\"");
                    builder.AppendLine($"$solutions = \"{solutionStr}\"");
                    builder.AppendLine("Invoke-Expression \" & '$solutionPacker' - s 'Build.sln' -p '$source' -i '$solutions'\"");
                    builder.AppendLine("}");
                }
                else
                {
                    builder.AppendLine($"\"{GetSolutionPackerExe()}\" -p \"{branchPath}\" -i \"{solutionStr}\" ");
                }
                return builder.ToString();
            }
            return string.Empty;
        }

        private string GetStaticScriptsEnds(ScriptExportType scriptType)
        {
            return string.Empty;
        }

        private string GetStaticScriptsStarts(ScriptExportType scriptType)
        {
            if (scriptType == ScriptExportType.Powershell && IsTeamFoundationPowerShellSnapInInstalled)
            {
                return Encoding.UTF8.GetString(Resources.PluginManager);
            }
            return string.Empty;
        }

        private static string CreateServiceFooter(ScriptExportType scriptType)
        {
            if (scriptType == ScriptExportType.Powershell)
                return "}" + Environment.NewLine;
            return string.Empty;
        }

        private static string CreateHeader(ScriptExportType scriptType, IOperationService service, string powerShellParams)
        {
            string operationHead = "rem --------" + service.OperationName.ToUpper() + "--------";
            if (scriptType == ScriptExportType.Powershell)
                operationHead =
                    $"# --------{service.OperationName.ToUpper()}--------{Environment.NewLine}{Environment.NewLine}function {service.GetPowershellFunctionName()}({powerShellParams}){Environment.NewLine}" +
                    "{";
            return operationHead;
        }

        private IEnumerable<string> GeneratePowershellMethodCalls(IOrderedEnumerable<IOperationService> includedServices, string powerShellParams, 
            IServiceSettings settings)
        {
            yield return "###########################MAIN CODE#####################################";
            if (settings.GetSettingsFromProvider<BuildServiceSettings>().BuildMode == BuildMode.MergedBuild)
                yield return "MergeSolutions";
            foreach (var service in includedServices)
                yield return $"{service.GetPowershellFunctionName()} {powerShellParams}";
            yield return "###########################END MAIN CODE#################################";
        }

        private string GetCallScriptMethodScript(ScriptExportType scriptType, string filePath)
        {
            if (File.Exists(filePath))
            {
                if (scriptType == ScriptExportType.Batch)
                    return ($"call \"{filePath}\"");
                if (scriptType == ScriptExportType.Powershell)
                    return ($"Invoke-Expression \"{filePath}\"");
            }
            return string.Empty;
        }

        private static bool TestPowershellSnapIn()
        {
            //var reg = new RegistryWatcher();
            try
            {
                var snapinPath = "SOFTWARE\\Microsoft\\PowerShell\\1\\PowerShellSnapIns";
                var snapInName = "TeamFoundation.PowerShell";
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(snapinPath).GetSubKeyNames().Any(s => s.Contains(snapInName))
                    || RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(snapinPath).GetSubKeyNames().Any(s => s.Contains(snapInName));
            }
            catch (Exception)
            {
                return false;
            }
        }



    }
}