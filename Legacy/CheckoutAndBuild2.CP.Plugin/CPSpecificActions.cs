using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using Microsoft.Build.Execution;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.Win32;

namespace CheckoutAndBuild2.CP.Plugin
{
    [Export(typeof(ICustomAction))]
    [Export(typeof(IScriptGenerator))]
    public class CPSpecificActions : ICustomAction, IScriptGenerator
    {
        private readonly Dictionary<string, string> backups = new Dictionary<string, string>();
        private readonly ITfsContext tfsContext;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public CPSpecificActions()
        {
            tfsContext = CPPlugin.Get<ITfsContext>();
        }

        public string GeneratePreScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings,
            ScriptExportType scriptExportType)
        {
            if (service.GetOperationType() == KnownOperation.Build)
            {
                var cpSettings = settings.GetSettingsFromProvider<CPSettings>();
                var builder = new StringBuilder();
                if (cpSettings.DeleteTypeLibs)
                    builder.AppendLine(GetTypeLibCleanScript(scriptExportType));
                var cc = solutions.FirstOrDefault(model => model.ItemPath.Contains(SolutionNames.ControlCenter));
                if (cc != null && cpSettings.RegisterCom)
                    builder.AppendLine(GetRegsvr32Script(scriptExportType,Path.Combine(cc.SolutionFolder, @"Assemblies\LicProtectorEasyGo264.dll")));

                return builder.ToString();
            }
            return string.Empty;
        }

        public string GeneratePostScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings,
            ScriptExportType scriptExportType)
        {
            return string.Empty;
        }

        public void RunPostAction(IOperationService service, ISolutionProjectModel solutionFile, object result,
            IServiceSettings settings)
        {
            switch (service.GetOperationType())
            {
                case KnownOperation.Clean:
                    RunPostCleanAction(solutionFile, settings);
                    break;
                case KnownOperation.Checkout:
                    RunPostCheckoutAction(solutionFile, result as GetStatus, settings);
                    break;
                case KnownOperation.Build:
                    RunPostBuildAction(solutionFile, result as BuildResult, settings);
                    break;
            }
        }

        public void RunPreAction(IOperationService service, ISolutionProjectModel solutionFile, IServiceSettings settings)
        {
            switch (service.GetOperationType())
            {
                case KnownOperation.Clean:
                    RunPreCleanAction(solutionFile, settings);
                    break;
                case KnownOperation.Checkout:
                    RunPreCheckoutAction(solutionFile, settings);
                    break;
                case KnownOperation.Build:
                    RunPreBuildAction(solutionFile, settings);
                    break;
            }
        }


        private void RunPreBuildAction(ISolutionProjectModel solution, IServiceSettings settings)
        {
            if (solution.SolutionFileName.ToLower().Contains(SolutionNames.PlannerEmbedded.ToLower()))
                CPFileHelper.KillProcess("CP-Suite");

            if (solution.SolutionFileName.ToLower().Contains(SolutionNames.ServerEmbedded.ToLower()))
                CPFileHelper.KillProcess("CP-Server");

            var cpSettings = settings.GetSettingsFromProvider<CPSettings>(solution);
            if (cpSettings.DeleteTypeLibs)
            {
                if (solution.ItemPath.Contains(SolutionNames.Common))
                    DeleteTypeLibs(solution);
            }
            if (cpSettings.RegisterCom)
            {
                if (solution.ItemPath.Contains(SolutionNames.ControlCenter))
                    RegisterLicProtectorEasyGo(solution.ItemPath);
            }
            if (solution.IsDelphiProject)
                RemoveReadOnly(Path.ChangeExtension(solution.ItemPath, ".res"), Path.ChangeExtension(solution.ItemPath, ".ridl"), Path.ChangeExtension(solution.ItemPath, ".tlb"));
        }
  
        private void RunPreCleanAction(ISolutionProjectModel solutionFile, IServiceSettings settings)
        {
            var cpSettings = settings.GetSettingsFromProvider<CPSettings>(solutionFile);
            if (cpSettings.AutoBackup)
            {
                try
                {
                    if (solutionFile.ItemPath.Contains(SolutionNames.ControlCenter))
                        CreateBackups(solutionFile, new[] {"MachineUserData", "UserData", "ProgramData"}).Wait();
                    if (solutionFile.ItemPath.Contains(SolutionNames.CPServer) || solutionFile.ItemPath.Contains(SolutionNames.ServerEmbedded))
                        CreateBackups(solutionFile, new[] {@"CP-AppServer\Modules\Operations Planning\xml"}).Wait();
                }
                catch (Exception e)
                {}
            }
        }

        private void RunPostBuildAction(ISolutionProjectModel solutionFile, BuildResult result, IServiceSettings settings)
        {
            if (solutionFile.ItemPath.Contains(SolutionNames.CPServer))
            {
                var target = new DirectoryInfo(Path.Combine(ExecsDir(solutionFile), Const.AppServerDirName, Const.ModulesDirName, Const.PlannerModuleDir, "manbackup"));
                if (!target.Exists)
                    target.Create();
            }
            var cpSettings = settings.GetSettingsFromProvider<CPSettings>(solutionFile);
            if (result.OverallResult == BuildResultCode.Success)
            {
                // kopiert wenn delphi kompiliert wurde alles aus dem delhi bin zum modules verzeichnis wo es benötigt wird.
                CopyFromBuildResultDirAsync(solutionFile).Wait();
            }

            if (cpSettings.AutoBackup)
                TryRestoreBackups(solutionFile).Wait();

            AutoUndo(solutionFile);
        }


        private void RunPostCleanAction(ISolutionProjectModel solutionFile, IServiceSettings settings)
        {
        }

        private void RunPreCheckoutAction(ISolutionProjectModel solutionFile, IServiceSettings settings)
        {
            AutoUndo(solutionFile);
        }

        private void AutoUndo(ISolutionProjectModel solutionFile)
        {
            if (solutionFile.ItemPath.Contains(SolutionNames.PlannerEmbedded) ||
                solutionFile.ItemPath.Contains(SolutionNames.PlannerEmbedded))
            {
                UndoIfChanged(solutionFile.SolutionFolder, "*.res");
            }

            if (solutionFile.ItemPath.Contains(SolutionNames.CPServer))
            {
                UndoIfChanged(solutionFile.SolutionFolder, "*.Generated.cs");
            }
        }

        private void UndoIfChanged(string folder, string searchPattern)
        {
            try
            {
                if (tfsContext?.SelectedWorkspace != null)
                {
                    tfsContext.SelectedWorkspace.GetPendingChanges(new[] {new ItemSpec(folder, RecursionType.Full)});
                    var regex = new Regex(Helper.WildcardToRegex(searchPattern));
                    var changes = tfsContext.SelectedWorkspace
                        .GetPendingChanges(new[] {new ItemSpec(folder, RecursionType.Full)})
                        .Where(change => regex.IsMatch(change.FileName)).ToArray();
                    if (changes.Any())
                        tfsContext.SelectedWorkspace.Undo(changes);
                }
            }
            catch (Exception)
            { }
        }

        private void RunPostCheckoutAction(ISolutionProjectModel solutionFile, GetStatus result, IServiceSettings settings)
        {
        }


        #region Private helpers

        private async Task TryRestoreBackups(ISolutionProjectModel solutionFile)
        {
            var tasks = new List<Task>();
            if (backups.Any())
            {
                foreach (var backup in backups.Where(pair => pair.Key.StartsWith(DevDir(solutionFile))).ToList())
                {
                    string source = backup.Value;
                    string target = backup.Key;
                    if (Directory.Exists(target))
                    {
                        backups.Remove(backup.Key);
                        tasks.Add(CopyAsync(new DirectoryInfo(source), new DirectoryInfo(target)).ContinueWith(task => DeleteDirectory(source)));
                    }
                }
            }
            await Task.WhenAll(tasks);
        }

        private async Task CreateBackups(ISolutionProjectModel solutionFile, string[] toBackup)
        {
            var tasks = new List<Task>();
            backups.Clear();
            foreach (var dir in GetToBackupSources(solutionFile, toBackup))
            {
                if (Directory.Exists(dir) && !backups.ContainsKey(dir))
                {
                    string target = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), Path.GetFileName(dir));
                    backups.Add(dir, target);
                    tasks.Add(CopyAsync(new DirectoryInfo(dir), new DirectoryInfo(target)));
                }
            }
            await Task.WhenAll(tasks);
        }

        private IEnumerable<string> GetToBackupSources(ISolutionProjectModel solutionFile, string[] toBackup)
        {
            foreach (var dirName in toBackup)
            {
                yield return Path.Combine(ExecsDir(solutionFile), dirName);
                yield return Path.Combine(TestsDir(solutionFile), dirName);
            }
        }

        private string DevDir(ISolutionProjectModel solutionFile)
        {
            if (!solutionFile.IsDelphiProject)
                return Path.Combine(new DirectoryInfo(solutionFile.SolutionFolder).Parent.Parent.FullName);
            return Path.Combine(new DirectoryInfo(solutionFile.SolutionFolder).Parent.Parent.Parent.FullName);
        }

        private string ExecsDir(ISolutionProjectModel solutionFile)
        {
            return Path.Combine(DevDir(solutionFile), Const.ExecsDirName);
        }

        private string TestsDir(ISolutionProjectModel solutionFile)
        {
            return Path.Combine(DevDir(solutionFile), Const.TestsDirName);
        }

        private async Task CopyFromBuildResultDirAsync(ISolutionProjectModel solutionFile)
        {
            if (solutionFile.IsDelphiProject && solutionFile.ItemPath.Contains(SolutionNames.ServerEmbedded))
            {
                var currentProjectDir = new DirectoryInfo(solutionFile.SolutionFolder);
                var sourceDir = new DirectoryInfo(Path.Combine(currentProjectDir.FullName, "bin"));
                var target = new DirectoryInfo(Path.Combine(ExecsDir(solutionFile), Const.AppServerDirName, Const.ModulesDirName, Const.PlannerModuleDir));
                await CopyAsync(sourceDir, target, true);
            }

            if (solutionFile.IsDelphiProject && solutionFile.ItemPath.Contains(SolutionNames.PlannerEmbedded))
            {
                var currentProjectDir = new DirectoryInfo(solutionFile.SolutionFolder);
                var sourceDir = new DirectoryInfo(Path.Combine(currentProjectDir.FullName, "ResDLL"));
                var target = new DirectoryInfo(Path.Combine(ExecsDir(solutionFile), Const.SuiteDirName, Const.ModulesDirName, Const.PlannerModuleDir));
                await CopyAsync(sourceDir, target, true);
            }
        }

        private async Task CopyAsync(DirectoryInfo source, DirectoryInfo target, bool overwrite = false)
        {
            if (source.Exists)
                await Task.Run(() => CPFileHelper.CopyFolderFast(source.FullName, target.FullName, true, !overwrite));
        }

        private string GetIntegrationName(WorkingFolder workingFolder, string prefix = "_last-")
        {
            string res = "Head";
            var splits = workingFolder.ServerItem.Split(new[] {"Features"}, StringSplitOptions.None);
            if (splits.Length == 2)
            {
                var items = splits[1].Split('/').Where(s => !string.IsNullOrEmpty(s));
                res = items.FirstOrDefault();
            }
            if (!string.IsNullOrEmpty(res))
                return $"{prefix}{res}";
            return null;
        }

        private DirectoryInfo GetIntegrationFolderPath(params string[] segments)
        {
            DirectoryInfo res = null;
            res = Directory.Exists(@"O:\#CPSoftware\development") ? new DirectoryInfo(@"O:\#CPSoftware\development") : new DirectoryInfo(@"\\cpws01b\work\#CPSoftware\development");
            if (segments != null && segments.Any())
                res = new DirectoryInfo(Path.Combine(res.FullName, Path.Combine(segments)));
            return res;
        }

        private void DeleteDirectory(string source)
        {
            try
            {
                Directory.Delete(source, true);
            }
            catch (Exception)
            {}
        }

        private void RemoveReadOnly(params string[] fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (File.Exists(fileName))
                {
                    var file = new FileInfo(fileName);
                    if (file.IsReadOnly)
                        file.IsReadOnly = false;
                }
            }
        }

        private void RegisterLicProtectorEasyGo(string solution)
        {
            try
            {
                string path = Path.GetDirectoryName(solution);
                if (path != null)
                {
                    string licDll = Path.Combine(path, @"Assemblies\LicProtectorEasyGo264.dll");
                    if (File.Exists(licDll))
                        CPFileHelper.Regsvr(licDll);
                    else
                        Trace.WriteLine("Error could not register LicProtectorEasyGo264.dll: File not found " + licDll);
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error could not register LicProtectorEasyGo264.dll: " + e.Message, "Lic");
            }
        }
        private void DeleteTypeLibs(ISolutionProjectModel model)
        {
            if (ShouldDeleteTypeLibs(model?.ParentWorkingFolder))
            {
                var p = Path.GetTempFileName() + ".bat";
                File.WriteAllText(p, GetTypeLibCleanScript(ScriptExportType.Batch));
                ProcessStartInfo startInfo = new ProcessStartInfo(p) {Verb = "runas"};
                try
                {
                    Process.Start(startInfo);
                }
                catch (Exception)
                {}
            }
        }

        private bool ShouldDeleteTypeLibs(WorkingFolder folder)
        {
            foreach (var libId in typeLibIds)
            {
                string path;
                var regpath = "TypeLib\\{" + libId + "}\\4.5\\0\\win32";
                if (RegistryHelper.TryReadValue<string>(RegistryHive.ClassesRoot, regpath, "", RegistryValueKind.String, out path))
                {
                    if (folder == null || !CPFileHelper.IsSubPathOf(path, folder.LocalItem))
                        return true;
                }
            }           
            return false;
        }

        private string[] typeLibIds = { "A0DCD767-2262-4228-9B52-A0642C9D6A75", "C5CE8735-5F4D-4C52-A999-6CBEEC5A2587" };
        private string GetTypeLibCleanScript(ScriptExportType scriptExportType)
        {
            var builder = new StringBuilder();
            if (scriptExportType == ScriptExportType.Batch)
            {
                foreach (string libId in typeLibIds)                
                    builder.AppendLine("regedit /d /s  \"HKEY_CLASSES_ROOT\\TypeLib\\{ "+ libId + "}\"");
                foreach (string libId in typeLibIds)
                    builder.AppendLine("rem reg Delete  \"HKEY_CLASSES_ROOT\\TypeLib\\{ "+libId+"}\" /va /f");
                //builder.AppendLine("regedit /d /s  \"HKEY_CLASSES_ROOT\\TypeLib\\{ A0DCD767 - 2262 - 4228 - 9B52 - A0642C9D6A75}\"");
                //builder.AppendLine("regedit /d /s   \"HKEY_CLASSES_ROOT\\TypeLib\\{ C5CE8735 - 5F4D - 4C52 - A999 - 6CBEEC5A2587}\"");
                //builder.AppendLine("rem reg Delete  \"HKEY_CLASSES_ROOT\\TypeLib\\{ A0DCD767 - 2262 - 4228 - 9B52 - A0642C9D6A75}\" /va /f");
                //builder.AppendLine("rem reg Delete  \"HKEY_CLASSES_ROOT\\TypeLib\\{ C5CE8735 - 5F4D - 4C52 - A999 - 6CBEEC5A2587}\" /va /f");
            }
            else            
                builder.AppendLine(Encoding.UTF8.GetString(Resources.UnregisterTypeLibs));
            
            return builder.ToString();
        }

        private string GetRegsvr32Script(ScriptExportType type, string dllFile)
        {
            if (type == ScriptExportType.Batch)
                return GetRegsvr32Bat(dllFile);
            return GetRegsvr32Ps1(dllFile);
        }

        private string GetRegsvr32Ps1(string dllFile)
        {
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string regsvr = Path.Combine(system, @"regsvr32.exe");
            var builder = new StringBuilder()
                .AppendLine($"$regsvr32 = \"{regsvr}\"");
            builder.AppendLine($"& $regsvr32 \"{dllFile}\" /s");
            return builder.ToString();
        }

        private string GetRegsvr32Bat(string dllFile)
        {
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string regsvr = Path.Combine(system, @"regsvr32.exe");
            return $"\"{regsvr}\" \"{dllFile}\" /s ";
        }

        #endregion
    }
}