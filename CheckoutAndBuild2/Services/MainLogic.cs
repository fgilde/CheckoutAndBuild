using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2.Services
{
    public class MainLogic : NotificationObject
    {
        private readonly IServiceProvider serviceProvider;
        private IOperationService currentOperation;
        private GlobalStatusService statusService => serviceProvider.Get<GlobalStatusService>();
        private SettingsService settingsService => serviceProvider.Get<SettingsService>();
        private static IList<DelegateCommand<IEnumerable<ProjectViewModel>>> serviceCommands;
        private ConcurrentBag<ISolutionProjectModel> cancelledSolutions;

        private bool isCompleteRunning;

        public bool IsCompleteRunning
        {
            get => isCompleteRunning;
            set => SetProperty(ref isCompleteRunning, value);
        }

        public bool IsAnyServiceRunning => serviceProvider.Get<MainViewModel>()?.CanCancel() ?? false;

        public IOperationService CurrentOperation
        {
            get => currentOperation;
            set => SetProperty(ref currentOperation, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public MainLogic(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public async Task RunCheckoutAndBuildAsync(WorkingFolderListViewModel listViewModel,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var includedProjects = listViewModel.WorkingFolders.SelectMany(model => model.Projects)
                .OrderBy(model => model.BuildPriority).ToList();

            await RunCheckoutAndBuild(includedProjects, cancellationToken);
        }

        public Task<Collection<PSObject>> ExecutePowershellScriptAsync(string filename, IOperationService service,
            ISolutionProjectModel[] projects, CancellationToken token = default(CancellationToken))
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            if (service != null)
                parameters.Add("service", service);
            parameters.Add("solutions", projects);
            return ScriptHelper.ExecutePowershellScriptAsync(filename, parameters, token);
        }

        public IEnumerable<IOperationService> GetIncludedServices(ISolutionProjectModel specificModel = null)
        {
            return CheckoutAndBuild2Package.GetExportedValues<IOperationService>().Where(service => service.IsIncluded(specificModel)).OrderBy(service => service.Order);
        }

        public async Task RunCheckoutAndBuild(IList<ProjectViewModel> includedProjects,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancelledSolutions = new ConcurrentBag<ISolutionProjectModel>();
            if (cancellationToken.IsCancellationRequested)
                return;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                var tasksToAwait = new List<Task>();                
                var settings = InitializeCompleteRun(includedProjects, cancellationToken);

                #region PreScript Handling

                if (settings.RunPreScriptsAsync)
                    tasksToAwait.Add(CheckAndRunScript(settings.PreBuildScriptPath, null, includedProjects, ScriptExecutionSettings.OneOutputStream, cancellationToken));
                else
                    await CheckAndRunScript(settings.PreBuildScriptPath, null, includedProjects, ScriptExecutionSettings.Default, cancellationToken);

                #endregion

                void PostBuildExecution(IOperationService s) => tasksToAwait.Add(CheckAndRunScript(settings.PostBuildScriptPath, s, includedProjects, ScriptExecutionSettings.OneOutputStream, cancellationToken));
                var operationServices = GetIncludedServices().ToArray();
                foreach (IOperationService service in operationServices)
                {
                    CurrentOperation = service;
                    try
                    {
                        var projects = includedProjects.Where(model => (CurrentOperation?.IsIncluded(model) ?? false) && !IsCancelled(model));
                        await cancellationToken.WaitWhenPaused();
                        var task = CurrentOperation?.ExecuteAsync(projects, settings, cancellationToken);
                        if (task != null)
                        {
                            #region PostBuildScript Handling

                            if (CurrentOperation?.ServiceId == ServiceIds.BuildServiceId.ToGuid())
                            {
                                if (settings.RunPostScriptsAsync)
                                    PostBuildExecution(service);
                                else
                                {
                                    await task.ContinueWith(task1 => PostBuildExecution(service), cancellationToken,
                                        TaskContinuationOptions.NotOnCanceled, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                                }
                            }

                            #endregion
                            await task;
                        }
                    }
                    catch (Exception e)
                    {
                        Output.WriteLine("Error in {0}-Service : {1}", CurrentOperation?.OperationName, e.Message);
                    }
                }

                if (tasksToAwait.Count > 0)
                {
                    statusService.InitOrAttach(tasksToAwait.Count, "Waiting for script completion");
                    await Task.WhenAll(tasksToAwait.Where(t => !t.IsCompleted && !t.IsCanceled));
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                FinalizeCompleteRun(includedProjects, cancellationToken, watch);
            }
        }

        private void FinalizeCompleteRun(IList<ProjectViewModel> includedProjects, CancellationToken cancellationToken, Stopwatch watch)
        {            
            IsCompleteRunning = false;
            statusService.Stop();
            serviceProvider.Get<MainViewModel>().InvalidateBusy();
            includedProjects.SetOperations(Operations.None);
            CurrentOperation = null;
            Output.WriteLine(string.Format("EVERYTHING IS {1} {0}", watch.Elapsed, cancellationToken.IsCancellationRequested ? "CANCELLED After" : "DONE!! in"));
        }

        private IServiceSettings InitializeCompleteRun(IList<ProjectViewModel> includedProjects, CancellationToken cancellationToken)
        {            
            cancellationToken.Register(() => CurrentOperation = null);
            cancellationToken.RegisterPaused((token, b) => RaisePropertyChanged(() => CurrentOperation));
            IsCompleteRunning = true;
            includedProjects.Apply(model => model.SetDefaultImageValues());
            Output.ClearTasks();
            IServiceSettings settings = settingsService.GetMainServiceSettings();
            statusService.InitOrAttach(GetActionCount(includedProjects, settings), "Init");
            return settings;
        }

        public bool IsCancelled(ISolutionProjectModel projectViewModel)
        {
            //TODO if (option single cancel for curreentservice only oder so) dann return false
            return cancelledSolutions != null && cancelledSolutions.Contains(projectViewModel);
        }

        public void Cancel(ISolutionProjectModel projectViewModel)
        {            
            //TODO if (option single cancel for curreentservice only oder so) dann nur CurrentOperation?.Cancel(projectViewModel); 
            projectViewModel.CurrentOperation = Operations.Cancelling;
            if (cancelledSolutions == null) cancelledSolutions = new ConcurrentBag<ISolutionProjectModel>();
            cancelledSolutions.Add(projectViewModel);
            CurrentOperation?.Cancel(projectViewModel);
            projectViewModel.CurrentOperation = Operations.None;


            // Wenn kein service oder keine Aktion läuft gesammten cancel auslösen
            var mainViewModel = serviceProvider.Get<MainViewModel>();
            var tokenSource = mainViewModel.CancellationTokenSource;
            var projects = mainViewModel.IncludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects).Concat(mainViewModel.ExcludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects));
            if (tokenSource != null && !tokenSource.Token.IsCancellationRequested &&
                projects.All(model => !model.IsBusy))
            {
                tokenSource.Cancel();
            }
        }
        

        public Task<string> GenerateExportScriptAsync(WorkingFolderListViewModel listViewModel, ScriptExportType scriptType,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return serviceProvider.Get<ScriptExportProvider>().GenerateExportScriptAsync(listViewModel, scriptType, cancellationToken);
        }


        public IList<DelegateCommand<IEnumerable<ProjectViewModel>>> GetServiceCommands(IEnumerable<ProjectViewModel> models)
        {
            return (from service in CheckoutAndBuild2Package.GetExportedValues<IOperationService>().OrderByDescending(service => service.Order)
                    let operation = service
                    select new DelegateCommand<IEnumerable<ProjectViewModel>>(operation.OperationName, m => ExecuteService(operation, models), enumerable => CanExecuteService(models), operation.ContextMenuImage)).ToList();
        }


        public IList<DelegateCommand<IEnumerable<ProjectViewModel>>> GetServiceCommands()
        {
            if (serviceCommands == null)
            {
                serviceCommands = (from service in CheckoutAndBuild2Package.GetExportedValues<IOperationService>().OrderByDescending(service => service.Order)
                                   let operation = service
                                   select new DelegateCommand<IEnumerable<ProjectViewModel>>(operation.OperationName, models => ExecuteService(operation, models), CanExecuteService, operation.ContextMenuImage)).ToList();
            }
            return serviceCommands;
        }
        
        public async void ExecuteService(IOperationService service, IEnumerable<ISolutionProjectModel> models)
        {
            if (models != null)
            {
                var solutionProjectModels = models as ISolutionProjectModel[] ?? models.ToArray();
                solutionProjectModels.Apply(model => model.SetDefaultImageValues());
                var mainViewModel = serviceProvider.Get<MainViewModel>();
                var cancellationToken = mainViewModel.CreateCancellationToken();
                Stopwatch watch = Stopwatch.StartNew();
                try
                {                    
                    await service.ExecuteAsync(solutionProjectModels, settingsService.GetMainServiceSettings(), cancellationToken);                                       
                }
                catch (Exception e)
                {
                    Output.Exception(e);
                }
                finally
                {                    
                    Output.WriteLine(string.Format("{0} {2} for {1} Projects {3} {4}", service.OperationName, solutionProjectModels.Length, cancellationToken.IsCancellationRequested ? "Cancelled" : "Done", cancellationToken.IsCancellationRequested ? "after" : "in", watch.Elapsed).ToUpper());
                    mainViewModel.InvalidateBusy();
                }
            }
        }

        #region Simple Help Methods

        private int GetActionCount(IList<ProjectViewModel> includedProjects, IServiceSettings settings)
        {
            int res = includedProjects.Select(model => GetIncludedServices(model).Count()).Sum();
            if (File.Exists(settings.PreBuildScriptPath))
                res++;
            if (File.Exists(settings.PostBuildScriptPath))
                res++;
            return res;
        }


        private async Task CheckAndRunScript(string scriptPath, IOperationService service, IEnumerable<ISolutionProjectModel> projects, ScriptExecutionSettings executionSettings,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
            {
                Output.WriteLine("RUN: {0}", Path.GetFileName(scriptPath));
                if (ScriptHelper.IsPowerShell(scriptPath))
                {
                    await ExecutePowershellScriptAsync(scriptPath, service, projects.ToArray(), cancellationToken);
                }
                else
                {
                    statusService.InitOrAttach(1, $"RUN: {Path.GetFileName(scriptPath)}");
                    var result = await ScriptHelper.ExecuteScriptAsync(scriptPath, "", executionSettings ?? ScriptExecutionSettings.Default, null, null, cancellationToken);
                    statusService.IncrementStep();
                    Output.WriteLine($"Script Result for {Path.GetFileName(scriptPath)}: {result.ProcessResult}");
                }
            }
        }

        private bool CanExecuteService(IEnumerable<ProjectViewModel> models)
        {
            return models != null && models.Any(model => !model.IsBusy);
        }

        #endregion

    }
}