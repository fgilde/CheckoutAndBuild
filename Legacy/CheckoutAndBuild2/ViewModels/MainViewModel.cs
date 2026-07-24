using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CheckoutAndBuild2.Contracts.Service;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.VisualStudio;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.Win32;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class MainViewModel : BaseViewModel, IDisposable
	{
		#region Privates		

		private bool isPaused;
		private bool registerProjectsBusyChange = true;
		private bool isStatusVisible;
		private ContextMenu extraCommands;
		private object extraContent;

		private readonly Dictionary<PropertyChangedEventHandler, INotifyPropertyChanged> registeredHandlers =
			new Dictionary<PropertyChangedEventHandler, INotifyPropertyChanged>();

		private bool isIntermediate;
	    internal PausableCancellationTokenSource CancellationTokenSource;

		#endregion

		public DelegateCommand<object> CheckoutAndBuildMainCommand { get; }
		public DelegateCommand<object> PauseCommand { get; }
		public DelegateCommand<object> ResumeCommand { get; }
		public DelegateCommand<object> CompleteCancelCommand { get; }
		public DelegateCommand<object> CancelCurrentOperationCommand { get; }

        public MainLogic MainLogic =>  GetService<MainLogic>();
		
		public MainViewModel(IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
			isIntermediate = true;			
			CheckoutAndBuildMainCommand = new DelegateCommand<object>(ExecuteCheckoutAndBuildRun, CanExecuteCheckoutAndBuildRun);
            PauseCommand = new DelegateCommand<object>(PauseCheckoutAndBuildRun, CanPauseCheckoutAndBuildRun);
            ResumeCommand = new DelegateCommand<object>(ResumeCheckoutAndBuildRun, CanResumeCheckoutAndBuildRun);
			CompleteCancelCommand = new DelegateCommand<object>(o=> Cancel(), CanCancelCheckoutAndBuildRun);
            CancelCurrentOperationCommand = new DelegateCommand<object>(o=> CancelCurrentOperation(), CanCancelCancelCurrentOperation);
			GlobalStatusService = serviceProvider.Get<GlobalStatusService>();

			ProfileSelectorViewModel = new ProfileSelectorViewModel(serviceProvider);
			WorkspaceSelectorViewModel = new WorkspaceSelectorViewModel(serviceProvider);
            GitBranchSelectorViewModel = new GitBranchSelectorViewModel(serviceProvider);

            BuildSettingsSelectorViewModel = new ServiceSettingsSelectorViewModel(serviceProvider);

			IncludedWorkingfolderModel = new WorkingFolderListViewModel(serviceProvider);
			ExcludedWorkingfolderModel = new WorkingFolderListViewModel(serviceProvider);			

			registeredHandlers.Add(this.OnChange(() => HasWorkspaceOrDirectory, b => Update()), this);
			registeredHandlers.Add(this.OnChange(() => IsBusy, b =>
			{
				CheckoutAndBuildMainCommand.RaiseCanExecuteChanged();
				UpdateStatusBarInfo(false);
			}), this);

            registeredHandlers.Add(GlobalStatusService?.OnChange(() => GlobalStatusService.IsActive, b => UpdateStatusBarInfo(true)), GlobalStatusService);            
		    registeredHandlers.Add(MainLogic?.OnChange(() => MainLogic.IsCompleteRunning, b => RaisePauseChanged(), true), MainLogic);
			IncludedWorkingfolderModel.ProjectsChanged += UpdateExcludedLists;
			ExcludedWorkingfolderModel.ProjectsChanged += UpdateIncludedLists;
		    MainLogic?.OnChange(() => MainLogic.CurrentOperation, service => CancelCurrentOperationCommand.RaiseCanExecuteChanged() );
			UpdateHeader();			
		}

	    private bool CanResumeCheckoutAndBuildRun(object arg)
	    {
	        return CanResume;
	    }
        
	    private bool CanPauseCheckoutAndBuildRun(object arg)
	    {
	        return CanPause;
	    }

        private void ResumeCheckoutAndBuildRun(object obj)
        {
            CancellationTokenSource.Resume();
            RaisePauseChanged();            
        }

        private void PauseCheckoutAndBuildRun(object obj)
	    {
            CancellationTokenSource.Pause();
	        RaisePauseChanged();
	    }

        private void RaisePauseChanged()
        {
            IsPaused = CancellationTokenSource?.IsPaused ?? false;
            RaisePropertiesChanged(() => CanPause, () => CanResume);
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            CancelCurrentOperationCommand.RaiseCanExecuteChanged();
        }

        public bool CanPause => CancellationTokenSource != null && !CancellationTokenSource.IsPaused && MainLogic.IsCompleteRunning;
	    public bool CanResume => CancellationTokenSource != null && CancellationTokenSource.IsPaused && MainLogic.IsCompleteRunning;

        public bool IsPaused
        {
            get => isPaused;
            set => SetProperty(ref isPaused, value);
        }


        public GlobalStatusService GlobalStatusService { get; }
		public WorkspaceSelectorViewModel WorkspaceSelectorViewModel { get; }
		public GitBranchSelectorViewModel GitBranchSelectorViewModel { get; }
		public ProfileSelectorViewModel ProfileSelectorViewModel { get; }
		public ServiceSettingsSelectorViewModel BuildSettingsSelectorViewModel { get; }		
		public WorkingFolderListViewModel IncludedWorkingfolderModel { get; }
		public WorkingFolderListViewModel ExcludedWorkingfolderModel { get; }

		public ContextMenu ExtraCommands
		{
			get => extraCommands;
		    set => SetProperty(ref extraCommands, value);
		}


		public object ExtraContent
		{
			get => extraContent;
		    set => SetProperty(ref extraContent, value);
		}

		public bool IsStatusVisible
		{
			get => isStatusVisible;
		    set => SetProperty(ref isStatusVisible, value);
		}

		public bool IsIntermediate
		{
			get => isIntermediate;
		    set => SetProperty(ref isIntermediate, value);
		}

		public CancellationToken CreateCancellationToken()
		{
			if (CancellationTokenSource == null || CancellationTokenSource.IsCancellationRequested)
			{                
                CancellationTokenSource = PausableCancellationTokenSource.CreateLinkedTokenSource(ModelCancellationTokenSource.Token);
                CancellationTokenSource.Token.Register(() =>
				{
					CompleteCancelCommand.RaiseCanExecuteChanged();
                    CancelCurrentOperationCommand.RaiseCanExecuteChanged();
					TrySetIsBusy(false);
					GlobalStatusService.Stop();
					Output.WriteLine("Operation cancelled");
					if (IsBusy)											
						IsBusy = false;
                    RaisePauseChanged();
                });
			}
            RaisePauseChanged();
            CompleteCancelCommand.RaiseCanExecuteChanged();
			return CancellationTokenSource.Token;
		}

	    public override void Update()
	    {
            ExtraContent = null;
            base.Update();
	    }
        
        private bool CanCancelCancelCurrentOperation(object arg)
        {
            return MainLogic.CurrentOperation != null && !CancellationTokenSource.IsCancellationRequested && !CancellationTokenSource.IsPaused;
        }

        private void CancelCurrentOperation()
        {
            MainLogic.CurrentOperation.Cancel();
        }


        protected override async void OnUpdateSynchronous(CancellationToken cancellationToken)
		{
            if (!cancellationToken.IsCancellationRequested) { 
                CheckoutAndBuildMainCommand.RaiseCanExecuteChanged();
			    ExtraCommands = GetExtraCommands().ToContextMenu();
		        if (HasWorkspace)
		        {
		            var hasSettings = await Task.Run(() => Settings.HasSettings(TfsContext.SelectedProfile, TfsContext.SelectedWorkspace), cancellationToken);
		            if (!hasSettings)
		                ShowCopySettingsPrompt();
		        }
            }
		}

	    private async void ShowCopySettingsPrompt()
	    {
	        var tfs = TfsContext;
	        var workspace = tfs?.SelectedWorkspace;
	        var profile = tfs?.SelectedProfile;
	        if (workspace != null)
	        {
                var otherWorkspaceWithSettingsExists = await Task.Run(() => tfs.GetWorkspaces().Any(w => Settings.HasSettings(profile, w)));
	            if (otherWorkspaceWithSettingsExists)
	            {
	                ExtraContent = new WorkspaceSettingsCopyControl(serviceProvider, (control, hasCopiedSettings) =>
	                {
	                    ExtraContent = null;
	                    if (hasCopiedSettings)
                            Update();
	                })
	                {
	                    Message = $"You didn't have configured any Settings for '{workspace.Name}'. Copy Settings from other Workspace?",
	                    TargetWorkspace = workspace
	                };
	            }
	        }
	    }

	    protected override void OnUpdateAsynchronous(CancellationToken cancellationToken)
		{
            if (!cancellationToken.IsCancellationRequested) { 
			    ExtraCommands = null;
			    var workspace = TfsContext.SelectedWorkspace;
			    if ((IsConnected && HasWorkspaceOrDirectory && workspace != null) || Directory.Exists(TfsContext.SelectedDirectory))
			    {
				    if (workspace != null) {
					    WorkingFolder[] workingFolders = workspace.Folders.Where(folder => !folder.IsCloaked).ToArray();
					    IncludedWorkingfolderModel.UpdateCollection(workingFolders.Select(folder => new WorkingFolderViewModel(folder, model => model.IsIncluded, serviceProvider){IsIncludedByDefault = true}).ToList());
					    ExcludedWorkingfolderModel.UpdateCollection(workingFolders.Select(folder => new WorkingFolderViewModel(folder, model => !model.IsIncluded, serviceProvider)).ToList());
				    }
				    else {
					    IncludedWorkingfolderModel.UpdateCollection(new[] { new WorkingFolderViewModel(TfsContext.SelectedDirectory, model => model.IsIncluded, serviceProvider) { IsIncludedByDefault = true } });
					    ExcludedWorkingfolderModel.UpdateCollection(new[] { new WorkingFolderViewModel(TfsContext.SelectedDirectory, model => !model.IsIncluded, serviceProvider) });
				    }	
				    if (registerProjectsBusyChange)
				    {
					    registerProjectsBusyChange = false;
                        try
                        {
                            IncludedWorkingfolderModel?.WorkingFolders?.SelectMany(model => model.Projects).Apply(model => model.OnChange(() => model.IsBusy, TrySetIsBusy));
                        }
                        catch{ }
				    }
			    }
			    WorkspaceSelectorViewModel.Update();
                GitBranchSelectorViewModel.Update();
			    UpdateHeader();
            }
		}


		#region Privates

		private IEnumerable<IUICommand> GetExtraCommands()
		{
			yield return new DelegateCommand<object>("Export as Batch Script...", ExportActionsAsBatchScript) {IconImage = Images.script_16xLG.ToImageSource()};
			yield return new DelegateCommand<object>("Export as Powershell Script...", ExportActionsAsPowershellScript) {IconImage = Images.PowerShell_5_0_icon.ToImageSource()};			
		}

	    private async void ExportActionsAsPowershellScript(object obj)
	    {
            await ExportActionsAsScriptAsync(ScriptExportType.Powershell);
        }

	    private async void ExportActionsAsBatchScript(object obj)
	    {
            await ExportActionsAsScriptAsync(ScriptExportType.Batch);
        }


	    private async Task ExportActionsAsScriptAsync(ScriptExportType scriptExportType)
		{
			CancellationTokenSource source = new CancellationTokenSource();
			var task = MainLogic.GenerateExportScriptAsync(IncludedWorkingfolderModel, scriptExportType, source.Token);
			var dialog = new SaveFileDialog {Filter = "Bat File | *.bat", FileName = "CheckoutAndBuild.bat", DefaultExt = ".bat"};
		    if (scriptExportType == ScriptExportType.Powershell)
		    {
		        dialog.Filter = "Powershell Script File | *.ps1";
		        dialog.FileName = "CheckoutAndBuild.ps1";
		        dialog.DefaultExt = ".ps1";

		    }
			if (dialog.ShowDialog() ?? false)
			{
				using (new PauseCheckedActionScope(() => IsBusy = true, () => TrySetIsBusy(false)))
				{
					var script = await task;
					File.WriteAllText(dialog.FileName, script);
					Output.Notification("Script with your settings is generated to " + dialog.FileName);
					VisualStudioDTE.TryOpenFile(dialog.FileName);
				}
			}
			else
			{
				if(!task.IsCompleted && !task.IsCanceled)
					source.Cancel();
			}
		}

        private void UpdateStatusBarInfo(bool syncIsBusy)
		{
			IsStatusVisible = GlobalStatusService.IsActive || IsBusy;
			IsIntermediate = !GlobalStatusService.IsActive;
			if (syncIsBusy)
				TrySetIsBusy(GlobalStatusService.IsActive);				
		}


		public void InvalidateBusy()
		{
			TrySetIsBusy(false);
		}

		private void TrySetIsBusy(bool _isBusy)
		{
			if (_isBusy)
				IsBusy = true;
			else
			{
				IsBusy = MainLogic.IsCompleteRunning || IncludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects).Any(model => model.IsBusy);
			}
		}

		private bool CanExecuteCheckoutAndBuildRun(object o)
		{
			return (IsConnected || HasWorkspaceOrDirectory) &&
				!IsBusy && IncludedWorkingfolderModel.WorkingFolders.SelectMany(model => model.Projects).Any();
		}

		private async void ExecuteCheckoutAndBuildRun(object o)
		{
			using (new PauseCheckedActionScope(() => TrySetIsBusy(true), () => TrySetIsBusy(false)))
			{
				var logic = MainLogic;				
				try
				{
					await logic.RunCheckoutAndBuildAsync(IncludedWorkingfolderModel, CreateCancellationToken());
				}
				finally
				{					
					CancellationTokenSource = null;
                    RaisePauseChanged();
					CompleteCancelCommand.RaiseCanExecuteChanged();				    
				}
			}
		}
	
	    public override void Cancel()
		{
			CancellationTokenSource.Cancel();		    
		}

		private bool CanCancelCheckoutAndBuildRun(object arg)
		{
			return CanCancel();
		}

		public override bool CanCancel()
		{
			return CancellationTokenSource != null
			       && !CancellationTokenSource.IsCancellationRequested
			       && !CancellationTokenSource.Token.IsCancellationRequested;
		}

		private void UpdateHeader()
		{
			if (IsConnected && HasWorkspaceOrDirectory)
			{
				IncludedWorkingfolderModel.Title = $"Included ({IncludedWorkingfolderModel.ProjectCount})";
				ExcludedWorkingfolderModel.Title = $"Excluded ({ExcludedWorkingfolderModel.ProjectCount})";
			}
			else
			{
				IncludedWorkingfolderModel.Title = "Included";
				ExcludedWorkingfolderModel.Title = "Excluded";
			}
		}

		private void UpdateIncludedLists(object sender, EventArgs<IEnumerable<ProjectViewModel>> e)
		{
			using (new PauseCheckedActionScope(() => IncludedWorkingfolderModel.ProjectsChanged -= UpdateExcludedLists,
				() => IncludedWorkingfolderModel.ProjectsChanged += UpdateExcludedLists))
			{
				foreach (WorkingFolderViewModel workingFolderViewModel in IncludedWorkingfolderModel.WorkingFolders)
					workingFolderViewModel.RaiseFilterChanged();
				UpdateHeader();
				CheckoutAndBuildMainCommand.RaiseCanExecuteChanged();
			}
		}

		private void UpdateExcludedLists(object sender, EventArgs<IEnumerable<ProjectViewModel>> eventArgs)
		{
			using (new PauseCheckedActionScope(() => ExcludedWorkingfolderModel.ProjectsChanged -= UpdateIncludedLists,
				() => ExcludedWorkingfolderModel.ProjectsChanged += UpdateIncludedLists))
			{
				foreach (WorkingFolderViewModel workingFolderViewModel in ExcludedWorkingfolderModel.WorkingFolders)
					workingFolderViewModel.RaiseFilterChanged();
				UpdateHeader();
				CheckoutAndBuildMainCommand.RaiseCanExecuteChanged();
			}
		}
		

		private void DetachHandlers()
		{
            foreach (KeyValuePair<PropertyChangedEventHandler, INotifyPropertyChanged> handler in registeredHandlers)
            {
                if(handler.Value != null && handler.Key != null)
                    handler.Value.PropertyChanged -= handler.Key;
            }
			registeredHandlers.Clear();
		}

		#endregion

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			DetachHandlers();
		}
	}

}