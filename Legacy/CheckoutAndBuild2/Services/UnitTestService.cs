using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
using FG.CheckoutAndBuild2.MSTest;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace FG.CheckoutAndBuild2.Services
{

	public class UnitTestService : BaseService, IOperationService
	{

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>		
		public UnitTestService(IServiceProvider serviceProvider)
			: base(serviceProvider)
		{ }

		public UnitTestService()
		{ }

		#region member of IOperationService

		public Guid ServiceId => ServiceIds.TestServiceId.ToGuid();

	    public int Order => ServicePriorities.UnitTestServicePriority;
	    public bool AllowScriptExport => true;

	    public ImageSource ContextMenuImage => Images.testrun.ToImageSource();

          public ScriptExportType[] SupportedScriptExportTypes => new[] { ScriptExportType.Batch, ScriptExportType.Powershell };
   
        public string GetScript(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, ScriptExportType exportType)
        {
            return string.Join(Environment.NewLine, solutionProjects.Select(model => exportType == ScriptExportType.Batch ? GetBatchScript(model, settings) : GetPowershellScript(model, settings)));
        }

        private string GetPowershellScript(ISolutionProjectModel solutionProject, IServiceSettings settings)
        {
            StringBuilder builder = new StringBuilder();
            foreach (Project project in solutionProject.GetUnitTestProjects())
            {
                var unitTestInfos = project.GetTestMethods().ToArray();                
                var command = GetTestCommand(solutionProject, settings, unitTestInfos, true);
                builder.AppendLine($"\"{command.MsTestProcess.MsTestPath}\" {command}");
            }
            return builder.ToString();
        }
        
	    private string GetBatchScript(ISolutionProjectModel solutionProject, IServiceSettings settings)
		{
			StringBuilder builder = new StringBuilder();
			foreach (Project project in solutionProject.GetUnitTestProjects())
			{
				var unitTestInfos = project.GetTestMethods().ToArray();
				//SetOrUpdateSettingsFile(projectViewModel, settings, unitTestInfos);		
				var command = GetTestCommand(solutionProject, settings, unitTestInfos, true);
				builder.AppendLine($"Invoke-Expression \"{command.MsTestProcess.MsTestPath}\" {command}");
			}
			return builder.ToString();
		}

		public string OperationName => "Unit Test";

	    protected override Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken)
	    {
            return RunAllTestsAsync(solutionProjects, settings, cancellationToken);
        }

	    #endregion

		public static bool NeedToBeCompiled(ISolutionProjectModel projectViewModel)
		{
			if (projectViewModel.IsDelphiProject)
				return false;
			var shouldCompile = projectViewModel.GetSolutionProjects().Any(project => project.IsDirty && project.IsBuildEnabled);
			if (shouldCompile)
				return true;
			shouldCompile = projectViewModel.GetUnitTestProjects().Select(project => project.GetOutputFile()).Any(info => !info.Exists);
			return shouldCompile;
		}

		public static bool CanRunAnyTests(ISolutionProjectModel projectViewModel)
		{
			return !projectViewModel.IsDelphiProject && projectViewModel.GetUnitTestProjects().Any();
		}

		public async Task RunAllTestsAsync(IEnumerable<ISolutionProjectModel> projectViewModels, IServiceSettings settings,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (cancellationToken.IsCancellationRequested)
				return;
            await cancellationToken.WaitWhenPaused();
            if (!serviceProvider.Get<MainLogic>().IsCompleteRunning)
				Output.ClearTasks();
			var viewModels = projectViewModels as ProjectViewModel[] ?? projectViewModels.ToArray();
			try
			{
				ClearAllTestResultsFromResultsWindow();
				statusService.InitOrAttach(viewModels.Length, $"Run Unittests for {viewModels.Length} Solutions");
				await Task.WhenAll(viewModels.Select(model => RunAllTestsAsync(model, settings, cancellationToken)));
			}
			catch (TaskCanceledException)
			{ }
			finally
			{
				foreach (var projectViewModel in viewModels.Where(model => model.ErrorContent == null))
					projectViewModel.SetDefaultImageValues();
			}
		}

		public async Task RunAllTestsAsync(ISolutionProjectModel projectViewModel, IServiceSettings settings, CancellationToken cancellationToken = default(CancellationToken))
		{
			var unitTestServiceSettings = settings.GetSettingsFromProvider<UnitTestServiceSettings>(projectViewModel);
            await cancellationToken.WaitWhenPaused();
            try
			{
				if (!IsCancelled(projectViewModel, cancellationToken) && CanRunAnyTests(projectViewModel))
				{
					if (NeedToBeCompiled(projectViewModel))
					{
						await serviceProvider.Get<LocalBuildService>().BuildSolutionsAsync(new[] { projectViewModel }, settings, cancellationToken);
						var buildErrors = Output.FindErrorTasks(projectViewModel);
						if (buildErrors.Any())
						{
							statusService.IncrementStep();
							return;
						}
					}
                    await cancellationToken.WaitWhenPaused();
                    var externalActionService = serviceProvider.Get<ExternalActionService>();
                    await externalActionService.RunExternalPreActions(projectViewModel, this, cancellationToken: cancellationToken);

				    void OnError(MsTestCommand command, string s)
				    {
				        if (!cancellationToken.IsCancellationRequested)
				            OnTestError(projectViewModel, command, s, unitTestServiceSettings.CancelOnFailures);
				    }

				    void TestData(MsTestCommand command, string s)
				    {
				        if (!cancellationToken.IsCancellationRequested)
				            OnTestData(projectViewModel, command, s, unitTestServiceSettings.CancelOnFailures);
				    }

				    await cancellationToken.WaitWhenPaused();
                    int totalTestCount = 0;
					using (new PauseCheckedActionScope(() => projectViewModel.CurrentOperation = GetOperationInfo(unitTestServiceSettings.TrackLiveOutput), () => projectViewModel.CurrentOperation = Operations.None, cancellationToken))
					{
						var watch = Stopwatch.StartNew();
						bool executed = false;

						//var unitTestInfos = projectViewModel.GetUnitTestProjects().SelectMany(project => project.GetTestMethods()).ToArray();
						var result = new Dictionary<TestRun, IList<MsTestError>>();
						foreach (Project project in projectViewModel.GetUnitTestProjects())
						{
							if (!cancellationToken.IsCancellationRequested)
							{
								var unitTestInfos = project.GetTestMethods().ToArray();
								int progress = 0;
								int total = unitTestInfos.Length;
								totalTestCount = totalTestCount + total;
								if (unitTestServiceSettings.TrackLiveOutput)
									projectViewModel.CurrentOperation.SetProgress(total, progress);

								var command = GetTestCommand(projectViewModel, settings, unitTestInfos);
								await command.ExecuteAsync((testCommand, s) =>
								{
									if (unitTestServiceSettings.TrackLiveOutput && !string.IsNullOrEmpty(s) && s.Contains("[testname]"))
										projectViewModel.CurrentOperation?.SetProgress(total, (++progress));
									TestData(testCommand, s);
								}, OnError, cancellationToken);
								executed = true;
								var results = OpenTestResultFileAndGetResults(project, command.ResultFile);
								if (results.Key != null)
									result.Add(results.Key, results.Value);
							}
						}
					
						watch.Stop();
						List<MsTestError> failedTests = result.SelectMany(pair => pair.Value).ToList();
						BuildErrorsViewModel errorModel = GetErrorsViewModel(projectViewModel);
						int passed = totalTestCount - failedTests.Count;
						if (errorModel != null && errorModel.Errors.Any())
							projectViewModel.SetResult(new ValidationResult(false, errorModel));						
						else
							projectViewModel.SetResult(ValidationResult.ValidResult);

                        await externalActionService.RunExternalPostActions(projectViewModel, this, passed == totalTestCount, cancellationToken: cancellationToken);
                        if (!cancellationToken.IsCancellationRequested)
						{
							Output.WriteLine(executed
								? string.Format("{2}/{3} Tests passed in testrun for {0} (completed in {1})", projectViewModel.SolutionFileName, watch.Elapsed, passed, totalTestCount)
								: $"There are no Tests for {projectViewModel.SolutionFileName}");
						}
					}
				}
				else
					Output.WriteLine($"There are no Tests for {projectViewModel.SolutionFileName}");
			}
			finally
			{
				statusService.IncrementStep();	
			}			
		}

		private MsTestCommand GetTestCommand(ISolutionProjectModel projectViewModel, IServiceSettings settings, UnitTestInfo[] unitTestInfos,
			bool isScriptExport = false)
		{
			var settingsSrv = serviceProvider.Get<SettingsService>();
			var tsp = CheckoutAndBuild2Package.GetGlobalService<IDefaultTestSettingsProvider>();
			var unitTestServiceSettings = settingsSrv.GetSettingsFromProvider<UnitTestServiceSettings>(projectViewModel);			
			var testSettingsFile = projectViewModel.EnsureAbsolutePath(settingsSrv.Get(projectViewModel.TestSettingsFileKey(), tsp != null ? tsp.GetTestSettingsFile(projectViewModel, settings) : String.Empty));
			var command = new MsTestCommand(unitTestInfos)
			{
				RequiresAdminPrivileges = unitTestServiceSettings.RequiresAdminPrivileges,
				SpecifyEachTest = unitTestServiceSettings.TrackLiveOutput && !isScriptExport
			};
			
			if (!string.IsNullOrEmpty(testSettingsFile) && File.Exists(testSettingsFile))
				command.DefaultTestSettings = testSettingsFile;	
			return command;
		}

		private BuildErrorsViewModel GetErrorsViewModel(ISolutionProjectModel project)
		{
			return new BuildErrorsViewModel(Output.FindErrorTasks(project), this, project, serviceProvider);
		}
		
		private void OnTestData(ISolutionProjectModel projectViewModel, MsTestCommand command, string data, bool cancelOnFailures)
		{
			if (data != null && (data.Contains("[Failed]") || data.Contains("[errormessage]")))
			{
				if (projectViewModel.CurrentOperation != null)				
					projectViewModel.CurrentOperation.ColorBrush = Brushes.DarkRed;		
				if(cancelOnFailures)
					serviceProvider.Get<MainViewModel>().CompleteCancelCommand.Execute(null);
			}
			Output.WriteLine(data, projectViewModel.SolutionFileName, command.ResultFile);
		}

		private void OnTestError(ISolutionProjectModel projectViewModel, MsTestCommand command, string errors, bool cancelOnFailures)
		{
			lock (lockObj)
			{
				if (projectViewModel.CurrentOperation != null && !string.IsNullOrEmpty(errors))
					projectViewModel.CurrentOperation.ColorBrush = Brushes.DarkRed;
				if (!string.IsNullOrEmpty(errors))
				{
					foreach (MsTestError error in MsTestError.Parse(errors))
					{
						UnitTestInfo testInfo = command.UnitTests.First(info => info.TestMethodName == error.TestName);
						string fileName = error.TestName;
						if (testInfo.ProjectFileItem != null && testInfo.ProjectFileItem.GetFile().Exists)
							fileName = testInfo.ProjectFileItem.GetFile().FullName;
						Output.WriteError($"{error.TestName}={error.ErrorMessage}", fileName, testInfo.Line, testInfo.Column, null, TaskCategory.User, testInfo.Project.FullPath, File.Exists(fileName));
					}
					Output.WriteLine(errors);
					if (cancelOnFailures)
						serviceProvider.Get<MainViewModel>().CompleteCancelCommand.Execute(null);
				}
			}
		}

		private OperationInfo GetOperationInfo(bool canTrackProgress)
		{
			return canTrackProgress ? Operations.UnitTesting : Operations.UnitTestingIndeterminate;
		}


		private void ClearAllTestResultsFromResultsWindow()
		{
			Check.TryCatch<Exception>(() =>
			{
				object mContext = PackageHelper.GetTestResultsToolWindowContextHelperInstance();
			    var allOptProp = mContext?.GetType().GetProperty("AllOptions", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			    if (allOptProp != null)
			    {
			        var testResultsToolWindowInstance = PackageHelper.GetTestResultsToolWindowInstance();
			        if (testResultsToolWindowInstance != null)
			        {
			            var clearMethod = testResultsToolWindowInstance.GetType().GetMethod("OnCloseRunInvoke", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			            var list = allOptProp.GetValue(mContext) as List<string>;
			            if (clearMethod != null && list != null && list.Count > 2)
			            {
			                for (int i = 2; i < list.Count; i++)
			                {
			                    SetTestRunOnResultsWindowToIndex(i);
			                    clearMethod.Invoke(testResultsToolWindowInstance, new[] { testResultsToolWindowInstance, EventArgs.Empty });
			                }
			            }
			        }
			    }
			});
		}

		private KeyValuePair<TestRun, List<MsTestError>> OpenTestResultFileAndGetResults(Project project, string fileName)
		{
			if (File.Exists(fileName))
			{
				VisualStudioDTE.TryOpenFile(fileName);
				var run = TestRun.LoadFromFile(fileName);
				List<MsTestError> failedTests = MsTestError.FromTestRun(run).ToList();
				foreach (var error in failedTests)
					Output.WriteError($"{error.TestName}={error.ErrorMessage}", error.TestPath, error.Line, 0, null, TaskCategory.User, project.FullPath, File.Exists(error.TestPath), false);								
				SetTestRunOnResultsWindowToIndex(1); // Alle läufe in einem ergebis fenster zeigen			
				return new KeyValuePair<TestRun, List<MsTestError>>(run, failedTests);
			}
			return new KeyValuePair<TestRun, List<MsTestError>>(null, new List<MsTestError>());
		}

		private void SetTestRunOnResultsWindowToIndex(int index)
		{
			Check.TryCatch<Exception>(() =>
			{
				object mContext = PackageHelper.GetTestResultsToolWindowContextHelperInstance();
			    var setSelectionIndexMethod = mContext?.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(info => info.Name == "SetSelection" && info.GetParameters().Length == 1 && info.GetParameters()[0].ParameterType == typeof(int));
			    setSelectionIndexMethod?.Invoke(mContext, new[] { (object)index });
			});
		}

	}
}