using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using EnvDTE80;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Converter;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.Services
{	
	public class CheckoutService : BaseService, IOperationService
	{		
		private string tfExe;

	    /// <summary>
	    /// Initializes a new instance of the <see cref="T:System.Object"/> class.
	    /// </summary>
	    public CheckoutService(IServiceProvider serviceProvider) : base(serviceProvider)
	    {}

	    public CheckoutService()
	    {}

		#region IOperationService

		public string OperationName => "Checkout";
	    public Guid ServiceId => ServiceIds.CheckoutServiceId.ToGuid();
	    public int Order => ServicePriorities.CheckoutServicePriority;
	    public bool AllowScriptExport => true;

	    public ImageSource ContextMenuImage => Images.CheckOutforEdit_13187.ToImageSource();

        public ScriptExportType[] SupportedScriptExportTypes => new[] { ScriptExportType.Batch, ScriptExportType.Powershell };

      
        public string GetScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, ScriptExportType exportType)
        {
            return string.Join(Environment.NewLine, solutionProjects.Select(model => exportType == ScriptExportType.Batch ? GetBatchScript(model, settings) : GetPowershellScript(model, settings)));
        }

        private string GetPowershellScript(ISolutionProjectModel solutionProject, IServiceSettings settings)
        {
            var context = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();
            if (solutionProject.ParentWorkingFolder == null)
            {                
                if (context.IsGitSourceControlled(solutionProject.ItemPath))
                {
                    string _s = Path.GetPathRoot(solutionProject.SolutionFolder).Replace("\\", string.Empty);
                    return "git pull \"" + _s + "\""; 
                }
                return string.Empty;
            }
            CheckoutServiceSettings serviceSettings = settings.GetSettingsFromProvider<CheckoutServiceSettings>(solutionProject);

            CheckTFExe();
            var builder = new StringBuilder()
                .AppendLine($"set-alias TFS \"{tfExe}\"");                
            var tfsCollection = context.TeamProjectCollection.Uri;
            var path = GetItemPath(solutionProject, serviceSettings, true);
            if (ScriptExportProvider.IsTeamFoundationPowerShellSnapInInstalled) {                 
                var basePath = context.TfsContextManager.CurrentContext.TeamProjectName;

                //CheckoutServiceSettings serviceSettings = settings.GetSettingsFromProvider<CheckoutServiceSettings>(solutionProject);
                //var versionSpec = settingsService.Get(SettingsKeys.VersionSpecKey, VersionSpec.Latest);
                //string versionSpecStr = versionSpec == VersionSpec.Latest ? "Latest" : versionSpec.DisplayString;
                 builder.AppendLine($"$tfsCollectionUrl = \"{tfsCollection}\"")
                    .AppendLine($"$teamProjectBasePath = \"{basePath}\"")
                    .AppendLine("$tfs=get-tfsserver $tfsCollectionUrl")                    
                    .AppendLine("$vCS = $tfs.GetService([Microsoft.TeamFoundation.VersionControl.Client.VersionControlServer])")
                    .AppendLine("$tfsProject = $vcs.GetTeamProject($teamProjectBasePath)")
                    .AppendLine($"$workSpaceName = \"{context.SelectedWorkspace.Name}\"")
                    .AppendLine("[Microsoft.TeamFoundation.VersionControl.Client.WorkSpace] $tfws = Get-TfsWorkspace -Server $tfs  -Name $workSpaceName")
                    .AppendLine($"$path=\"{path}\"")
                    .AppendLine($"TFS get $path /recursive")
                    .AppendLine($"TFS resolve $path /prompt");
                return builder.ToString();
            }
            
            string flags = GetFlags(serviceSettings);

            builder.AppendLine($"& TFS get \"{path}\" {flags} /recursive");
            return builder.ToString();
        }

	    private void CheckTFExe()
	    {
	        if (!File.Exists(tfExe))
	        {
	            var visualStudioInfo = VisualStudioPluginHelper.GetInstalledStudios().Last();
                if(visualStudioInfo.Version < Version.Parse("15.0")) 
	                tfExe = Path.Combine(visualStudioInfo.Path, "Common7", "IDE", "TF.exe");
                else
                    tfExe = Path.Combine(visualStudioInfo.Path, "Common7", "IDE", "CommonExtensions", "Microsoft", "TeamFoundation", "Team Explorer", "TF.exe");
            }
	    }

	    private string GetBatchScript(ISolutionProjectModel solutionProject, IServiceSettings settings)
		{
			if (solutionProject.ParentWorkingFolder == null)
			{
				var context = CheckoutAndBuild2Package.GetGlobalService<TfsContext>();
				if (context.IsGitSourceControlled(solutionProject.ItemPath))
				{
					string _s = Path.GetPathRoot(solutionProject.SolutionFolder).Replace("\\", string.Empty) + Environment.NewLine;
					_s += $"cd \"{solutionProject.SolutionFolder}\"" + Environment.NewLine;
					//_s += "git checkout master";  // TODO Mal richtig für git auschecken und branch ermitteln
					_s += "git pull";
					return _s;					
				}
				return string.Empty;
			}

            CheckTFExe();
			CheckoutServiceSettings serviceSettings = settings.GetSettingsFromProvider<CheckoutServiceSettings>(solutionProject);

            var path = GetItemPath(solutionProject, serviceSettings, false);

		    string s = Path.GetPathRoot(solutionProject.ParentWorkingFolder.LocalItem).Replace("\\", string.Empty);
            s += Environment.NewLine + $"cd \"{solutionProject.ParentWorkingFolder.LocalItem}\"" + Environment.NewLine;		    
		    s += $"\"{tfExe}\" get \"{path}\" /recursive {GetFlags(serviceSettings)} ";
			return s;
		}

	    private static string GetItemPath(ISolutionProjectModel solutionProject, CheckoutServiceSettings serviceSettings, bool local)
	    {
	        string path;
	        if (local)
	        {
                path = solutionProject.SolutionFolder;
                if (serviceSettings.CheckoutWorkingfolder)
                    path = solutionProject.ParentWorkingFolder.LocalItem;
                return path;
            }
	        path = solutionProject.ServerItem.Replace(solutionProject.SolutionFileName, string.Empty);
	        if (serviceSettings.CheckoutWorkingfolder)
	            path = solutionProject.ParentWorkingFolder.ServerItem;
	        return path;
	    }

	    private string GetFlags(CheckoutServiceSettings serviceSettings)
	    {
	        string promptFlag = "/noprompt";
	        string forceOverwriteFlag = "";
            if (serviceSettings.ForceAndOverwrite)
	            forceOverwriteFlag = "/force /overwrite";
	        if (serviceSettings.PromptForMerge)
	            promptFlag = "";
	        return $"{promptFlag} {forceOverwriteFlag}";
	    }

	    #endregion

        protected override Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken)
        {
            return CheckoutSolutionsAsync(solutionProjects, settings, cancellationToken);
        }

        public async Task CheckoutSolutionsAsync(IEnumerable<ISolutionProjectModel> projectViewModels, IServiceSettings settings,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
				return;
		    await cancellationToken.WaitWhenPaused();
            var viewModels = projectViewModels as ISolutionProjectModel[] ?? projectViewModels.ToArray();
			var versionSpec = settingsService.Get(SettingsKeys.VersionSpecKey, VersionSpec.Latest);
			statusService.InitOrAttach(viewModels.Length, $"Checkout (Get {VersionSpecToStringConverter.GetReadableString(versionSpec)})");
			try
			{
				GetStatus[] getStatuses;
				List<string> pathFilter = new List<string>();
				var checkoutServiceSettings = settings.GetSettingsFromProvider<CheckoutServiceSettings>();
				if (mainLogic.IsCompleteRunning && checkoutServiceSettings.CheckoutWorkingfolder)
				{
					using (new PauseCheckedActionScope(() => viewModels.SetOperations(Operations.Checkout), () => viewModels.SetOperations(Operations.None), cancellationToken))
					{                        
                        var workingFolders = viewModels.Select(model => model.ParentWorkingFolder).Distinct().ToArray();
						getStatuses = await Task.WhenAll(workingFolders.Select(folder => GetLatestVersionAsync(folder, settings, versionSpec, cancellationToken)));
						pathFilter.AddRange(workingFolders.Select(folder => folder.LocalItem));
						viewModels.Apply(model => statusService.IncrementStep());                        
                    }
				}
				else {
					pathFilter.AddRange(viewModels.Select(m => m.SolutionFolder));
					getStatuses = await Task.WhenAll(viewModels.Select(model => CheckoutSolutionAsync(model, settings, versionSpec, cancellationToken)));					
				}

				if (checkoutServiceSettings.PromptForMerge && getStatuses.Any(status => status != null && status.NumConflicts > 0))
					Application.Current.Dispatcher.Invoke(() => { TeamControlFactory.ResolveConflictsVS(tfsContext.SelectedWorkspace, pathFilter.ToArray());});		
			}
			catch (TaskCanceledException)
			{ }
		}
    
        public async Task<GetStatus> CheckoutSolutionAsync(ISolutionProjectModel projectViewModel, 
			IServiceSettings settings = null, VersionSpec versionSpec = null, CancellationToken cancellationToken = default(CancellationToken))
		{
            await cancellationToken.WaitWhenPaused();
            if (settings == null)
				settings = settingsService.GetMainServiceSettings();
			if (versionSpec == null)
				versionSpec = settingsService.Get(SettingsKeys.VersionSpecKey, VersionSpec.Latest);
			if (!IsCancelled(projectViewModel, cancellationToken))
			{
				try
				{
					using (new PauseCheckedActionScope(() => projectViewModel.CurrentOperation = Operations.Checkout, () => projectViewModel.CurrentOperation = Operations.None, cancellationToken))
					{
						var externalActionService = serviceProvider.Get<ExternalActionService>();
                        await externalActionService.RunExternalPreActions(projectViewModel, this, settings, cancellationToken);
						GetStatus status = await Task.Run(() => GetLatestVersionAsync(projectViewModel, settings, versionSpec, cancellationToken), cancellationToken);					
						await externalActionService.RunExternalPostActions(projectViewModel, this, status, settings, cancellationToken);
                        return status;
					}
				}
				finally { statusService.IncrementStep();}
			}
			return null;
		}

		private async Task<GetStatus> GetLatestVersionAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings, VersionSpec versionSpec, 
			CancellationToken cancellationToken = default (CancellationToken))
		{
			ItemSpec itemSpec = new ItemSpec(projectViewModel.SolutionFolder, RecursionType.Full);
            await cancellationToken.WaitWhenPaused();
            return await GetLatestVersionAsync(itemSpec, versionSpec, settings, projectViewModel, cancellationToken);
		}

		private async Task<GetStatus> GetLatestVersionAsync(WorkingFolder workFolder, IServiceSettings settings, VersionSpec versionSpec,
			CancellationToken cancellationToken = default (CancellationToken))
		{
			ItemSpec itemSpec = new ItemSpec(workFolder.ServerItem, RecursionType.Full);
			return await GetLatestVersionAsync(itemSpec, versionSpec, settings, null, cancellationToken);
		}

		private async Task<GetStatus> GetLatestVersionAsync(ItemSpec itemSpec, VersionSpec versionSpec, IServiceSettings settings, object userData, 
			CancellationToken cancellationToken = default (CancellationToken))
		{
			ISolutionProjectModel projectModel = userData as ISolutionProjectModel;

		    var workspace = tfsContext.SelectedWorkspace;
		    var space = tfsContext.VersionControlServer?.TryGetWorkspace(itemSpec.Item);
		    if (space != null)
		        workspace = space;

            if (workspace == null || tfsContext.IsGitSourceControlled(projectModel.ItemPath) || (tfsContext.VersionControlServer == null && !tfsContext.IsTfsConnected && tfsContext.SelectedWorkspace == null))
			{
				var dte = serviceProvider.Get<DTE2>();
				if (dte?.SourceControl != null)
					return await CheckoutWithDTESourceControlProviderAsync(dte, itemSpec);
			    
			    var args = $"-C \"{projectModel.ItemPath}\" pull";
			    ScriptHelper.ExecuteScript("git", args, ScriptExecutionSettings.Default, Output.WriteLine);
            }

            await cancellationToken.WaitWhenPaused();
            var request = new GetRequest(itemSpec, versionSpec);
			var checkoutServiceSettings = settings.GetSettingsFromProvider<CheckoutServiceSettings>(projectModel);
			bool forceAndOverwrite = checkoutServiceSettings.ForceAndOverwrite;
			GetOptions getOptions = forceAndOverwrite ? GetOptions.GetAll | GetOptions.Overwrite : GetOptions.None;

			GetFilterCallback filterCallback;
			if (cancellationToken.IsCancellationRequested)
				return null;
			using (TrackProgress(projectModel, out filterCallback, cancellationToken))
			{
				if (cancellationToken.IsCancellationRequested)
					return null;
                await cancellationToken.WaitWhenPaused();
                var status = await workspace.GetAsync(request, getOptions, filterCallback, userData, cancellationToken);
				Output.WriteLine(string.Empty);
				var sb = new StringBuilder();
				Output.WriteLine("Get Version: " + VersionSpecToStringConverter.GetReadableString(versionSpec) + " '" + itemSpec.Item + "'");
				sb.AppendDictionary(ReflectionHelper.GetProperties(status));
				Output.WriteLine(sb.ToString());

				foreach (Failure failure in status.GetFailures())
					Output.WriteError($"Fail:{failure.Message} with code {failure.Code}", failure.LocalItem);
				if (status.NumConflicts > 0 && !checkoutServiceSettings.PromptForMerge)
					Output.WriteWarning($"{status.NumConflicts} Checkout Conflicts for {itemSpec.Item}", itemSpec.Item);
				return status;	
			}
		}

		public async Task<GetStatus> CheckoutWithDTESourceControlProviderAsync(DTE2 dte, ItemSpec itemSpec)
		{
			GetStatus result = ExposedObject.New(typeof (GetStatus));
			var fails = new List<Failure>();
		    Output.WriteLine("Checkout: {0}", itemSpec.Item);
		    var checkOutItem = await Task.Run(() => dte.SourceControl.CheckOutItem(itemSpec.Item));
		    if (!checkOutItem)
		        fails.Add((Failure)ExposedObject.New(typeof(Failure), "Error for " + itemSpec.Item, SeverityType.Error));
            //if (dte.SourceControl.IsItemUnderSCC(itemSpec.Item))
            //{
            //	Output.WriteLine("Checkout: {0}", itemSpec.Item);
            //	var checkOutItem = await Task.Run(() => dte.SourceControl.CheckOutItem(itemSpec.Item));
            //	if (!checkOutItem)
            //		fails.Add((Failure)ExposedObject.New(typeof(Failure), "Error for " + itemSpec.Item, SeverityType.Error));
            //}
            //else
            //{
            //	string message = $"WARNING '{itemSpec.Item}' is not under SourceControl 0 files checked out";
            //	Output.WriteLine(message);
            //	fails.Add((Failure)ExposedObject.New(typeof(Failure), message, SeverityType.Error));
            //}

            if (fails.Any())
			{			
				var fieldInfo = result.GetType().GetField("m_failures",  BindingFlags.GetField |  BindingFlags.Instance | BindingFlags.NonPublic);
			    var failures = fieldInfo?.GetValue(result) as List<Failure>;
			    failures?.AddRange(fails);
			}
			return result;
		}


		private IDisposable TrackProgress(ISolutionProjectModel projectViewModel, out GetFilterCallback filterCallback, 
			CancellationToken cancellationToken = default (CancellationToken))
		{
			filterCallback = (workspace, operations, data) => {};
			if (cancellationToken.IsCancellationRequested || projectViewModel == null)
				return new DisposableCookie(() => { });

			double total = 0;
			double progress = 0;
			
			var server = tfsContext.VersionControlServer;
			projectViewModel.ResetProgress();
			filterCallback = (workspace, operations, data) =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;
				total = operations.Length + 4;
			};

			var projectBaseServerPath = Path.GetDirectoryName(projectViewModel.ServerItem).Replace(@"\", "/");
			GettingEventHandler handler = (sender, e) =>
			{
				var model = projectViewModel;
				if(cancellationToken.IsCancellationRequested)
					return;
				if (e.SourceServerItem == projectBaseServerPath || e.SourceServerItem.StartsWith(projectBaseServerPath))
				{
					model.CurrentOperation?.SetProgress(total, (++progress));
					Output.WriteLine(e.Status + " " + e.ServerItem);
				}
			};

			cancellationToken.Register(() => server.Getting -= handler);
			server.Getting += handler;

			return new DisposableCookie(() =>
			{
				server.Getting -= handler;
			});
		}	   
	}
}