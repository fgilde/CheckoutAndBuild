using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using Microsoft.Build.Execution;

namespace FG.CheckoutAndBuild2.Services
{
	public class ExternalActionService : BaseService
	{

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public ExternalActionService(IServiceProvider serviceProvider) : base(serviceProvider)
		{}

        public IDictionary<string, string> GetDefaultBuildProperties(ISolutionProjectModel project, IServiceSettings settings)
        {
            IDictionary<string, string> res = new Dictionary<string, string>();
            return CheckoutAndBuild2Package.GetExportedValues<IProjectBuildPropertiesProvider>().Select(provider => provider.GetDefaultBuildProperties(project, settings)).Where(props => props != null).Aggregate(res, (current, props) => current.MergeWith(props));
        }

        protected override async Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken)
        {
            var solutionProjectModels = solutionProjects as ISolutionProjectModel[] ?? solutionProjects.ToArray();
            await Task.WhenAll(solutionProjectModels.Select(folder => RunExternalPreActions(folder, null, settings, cancellationToken)));
            await Task.WhenAll(solutionProjectModels.Select(folder => RunExternalPostActions(folder, null, null, settings, cancellationToken)));
        }

        public async Task RunExternalPreActions(ISolutionProjectModel model, IOperationService service, IServiceSettings settings = null,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
				return;
			if (settings == null)
				settings = settingsService.GetMainServiceSettings();

		    await Task.WhenAll(RunPreServiceScriptFileForProjectAsync(service, model, settings, cancellationToken),
                Task.Run(() => {
                    foreach (ICustomAction externalAction in CheckoutAndBuild2Package.GetExportedValues<ICustomAction>())
                        externalAction.RunPreAction(service, model, settings);                        
                }, cancellationToken)
            );
		}

		public async Task RunExternalPostActions(ISolutionProjectModel model, IOperationService service, object result, IServiceSettings settings = null,
			CancellationToken cancellationToken = default(CancellationToken))
		{			
			if (cancellationToken.IsCancellationRequested)
				return;

			if (settings == null)
				settings = settingsService.GetMainServiceSettings();
            
		    await Task.WhenAll(
                RunPostServiceScriptFileForProjectAsync(service, model, result, settings, cancellationToken),
		        Task.Run(() =>
		        {
		            foreach (ICustomAction externalAction in CheckoutAndBuild2Package.GetExportedValues<ICustomAction>())
		            {
		                if (cancellationToken.IsCancellationRequested)
		                    break;
                        externalAction.RunPostAction(service, model, result, settings);                       
		            }
		        }, cancellationToken));


		}

		
        private Task RunScriptFileForProjectAsync(string fileName, IOperationService service, ISolutionProjectModel project, 
            object result = null, CancellationToken token = default(CancellationToken))
        {
            if (File.Exists(fileName))
            {
                Output.WriteLine($"Run Script {fileName}");
                if (ScriptHelper.IsPowerShell(fileName))
                {
                    var parameters = new Dictionary<string, object>
                    {
                        {"service", service},
                        {"solutionPath", project.ItemPath},
                        {"solutionObject", project},
                     };
                    if (result != null)
                        parameters.Add("result", result);
                    return ScriptHelper.ExecutePowershellScriptAsync(fileName, parameters, token);
                }
                var buildResult = result as BuildResult;
                if (buildResult == null || buildResult.OverallResult == BuildResultCode.Success)
                    return ScriptHelper.ExecuteScriptAsync(fileName, project.ItemPath, ScriptExecutionSettings.Default, cancellationToken: token);
            }
            return Task.Delay(0, token);
        }

        private Task RunPreServiceScriptFileForProjectAsync(IOperationService service, ISolutionProjectModel project, IServiceSettings settings,
            CancellationToken token = default(CancellationToken))
        {
            var misc = settings.GetSettingsFromProvider<MiscellaneousSettings>(project);
            if (File.Exists(misc.PreServiceScriptFile))
                Output.WriteLine($"Run Pre-{service.OperationName} Script for {project.SolutionFileName}");
            return RunScriptFileForProjectAsync(misc.PreServiceScriptFile, service, project, null, token);
        }

        private Task RunPostServiceScriptFileForProjectAsync(IOperationService service, ISolutionProjectModel project, object result, IServiceSettings settings,
            CancellationToken token = default(CancellationToken))
        {
            var misc = settings.GetSettingsFromProvider<MiscellaneousSettings>(project);
            if(File.Exists(misc.PostServiceScriptFile))
                Output.WriteLine($"Run Post-{service.OperationName} Script for {project.SolutionFileName}");
            return RunScriptFileForProjectAsync(misc.PostServiceScriptFile, service, project, result, token);
        }
    }
    


}