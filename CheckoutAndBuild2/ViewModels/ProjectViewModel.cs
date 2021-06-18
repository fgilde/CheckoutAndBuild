using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using EnvDTE;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Controls.Transition;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Services;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Process = System.Diagnostics.Process;
using Project = Microsoft.Build.Evaluation.Project;
using Window = System.Windows.Window;

namespace FG.CheckoutAndBuild2.ViewModels
{
    [DebuggerDisplay("{ItemPath}")]
    public class ProjectViewModel : BaseViewModel, ISolutionProjectModel
    {
        #region Private backing fields

        private bool isManuallyAdded;
        private bool isInOptionsView;
        private ProjectViewModelOptions projectOptionsControl;
        private List<Project> containingProjectsFallBack;
        private ObservableCollection<CheckBox> serviceSelectors;
        private bool canOpenPopup = true;
        private bool popUpEventsRegistered;
        private PendingChange[] pendingChanges;
        private ImageSource iconImage;
        private bool isIncluded;
        private int buildPriority;
        private OperationInfo currentOperation;
        private Geometry imageGeometry;
        private string serverItem;
        private Brush imageBrush;
        private object errorContent;
        private ProjectCollection projects;
        private IReadOnlyCollection<Project> unitTests;
        private int projectsBuildCompleted;
        #endregion

        private ValidationResult lastResult;
        private ProjectViewModelProperties properties;

        public MainViewModel MainViewModel => GetService<MainViewModel>();

        public IUICommand ShowErrorContentCommand { get; }
        public IUICommand ShowOptionsPopupCommand { get; }
        public IUICommand ShowBuildPropertiesCommand { get; }
        public IUICommand ShowBuildTargetsCommand { get; }
        public IUICommand RemoveCustomSolutionCommand { get; }
        public Common.Commands.DelegateCommand<PageTransition> SlideToOptionsCommand { get; }

        public Common.Commands.DelegateCommand<IOperationService> CancelCommand { get; }


        public ProjectViewModel(string projectFilePath, IServiceProvider serviveProvider, WorkingFolderViewModel parentWorkingFolder)
            : base(serviveProvider)
        {
            ShowErrorContentCommand = new Common.Commands.DelegateCommand<object>(ShowErrorContent, CanShowErrorContent);
            ShowOptionsPopupCommand = new Common.Commands.DelegateCommand<Popup>(TogglePopup, CanTogglePopup);
            ShowBuildPropertiesCommand = new Common.Commands.DelegateCommand<object>(EditBuildProperties);
            ShowBuildTargetsCommand = new Common.Commands.DelegateCommand<object>(EditBuildTargets);
            SlideToOptionsCommand = new Common.Commands.DelegateCommand<PageTransition>(ExecuteSlideToOptions);
            RemoveCustomSolutionCommand = new Common.Commands.DelegateCommand<object>(RemoveCustomSolution, CanRemoveCustomSolution);
            CancelCommand = new Common.Commands.DelegateCommand<IOperationService>(CancelOperation, CanCancelOperation);

            ItemPath = projectFilePath;
            ParentWorkingFolder = parentWorkingFolder;
            SetDefaultImageValues();
            var settings = Settings;
            if (settings != null)
            {
                isIncluded = settings.Get(this.IsIncludedKey(), GetIsProjectDefaultIncluded());
                buildPriority = settings.Get(this.BuildPriorityKey(), GetDefaultBuildPriority());
            }
            BeginLoadPendingChanges();
        }

        private bool GetIsProjectDefaultIncluded()
        {
            foreach (var defaultBehavior in CheckoutAndBuild2Package.GetExportedValues<IDefaultBehavior>())
            {
                var isIncluded = defaultBehavior.ShouldIncludedByDefault(this);
                if (isIncluded != null)
                    return isIncluded.Value;
            }
            return !IsDelphiProject;
        }

        private bool CanCancelOperation(IOperationService arg)
        {
            var mainLogic = GetService<MainLogic>();
            return IsBusy && CurrentOperation != null && CurrentOperation != Operations.None && CurrentOperation != Operations.Cancelling && mainLogic.IsAnyServiceRunning;
        }

        private void CancelOperation(IOperationService obj)
        {
            GetService<MainLogic>().Cancel(this);
        }

        public object OptionsObject => Settings.GenerateSettingsObjectForInspector(this, Properties);

        public ProjectViewModelProperties Properties => properties ?? (properties = new ProjectViewModelProperties(this));

        public bool IsManuallyAdded
        {
            get => isManuallyAdded;
            set => SetProperty(ref isManuallyAdded, value);
        }


        public bool IsInOptionsView
        {
            get => isInOptionsView;
            set => SetProperty(ref isInOptionsView, value);
        }

        public ObservableCollection<CheckBox> ServiceSelectors
        {
            get => serviceSelectors;
            set => SetProperty(ref serviceSelectors, value);
        }

        private int maxServiceCaptionLength = 40;

        public string ServicesCaptionSmall => (maxServiceCaptionLength == 0 || ServicesCaption.Length > maxServiceCaptionLength) ? string.Join(string.Empty, ServicesCaption.Take(maxServiceCaptionLength)) + "..." : ServicesCaption;

        public string ServicesCaption
        {
            get
            {
                var services = GetService<MainLogic>().GetIncludedServices(this).ToList();
                if (!services.Any())
                    return "(None)";
                return string.Join(",", services.Select(service => service.OperationName));
            }
        }

        internal async void BeginLoadPendingChanges()
        {
            var cancellation = new CancellationTokenSource();
            var tfs = TfsContext;
            if (tfs?.SelectedWorkspace != null)
            {
                var workspace = tfs.SelectedWorkspace;
                tfs.SelectedWorkspaceChanged += (sender, args) => cancellation.Cancel();
                if (tfs.IsTfsConnected && workspace != null)
                {
                    try
                    {
                        PendingChanges = await Task.Run(() =>
                        {
                            if (!cancellation.IsCancellationRequested)
                            {
                                ItemSpec itemSpec = new ItemSpec(SolutionFolder, RecursionType.Full);
                                return workspace.GetPendingChanges(new[] { itemSpec });
                            }
                            return new PendingChange[0];
                        }, cancellation.Token);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }


        public string BuildTargetsCaption => $"Build Targets ({BuildTargets.Count()})...";


        public IEnumerable<string> BuildTargets
        {
            get
            {
                var res = Settings.Get(this.BuildTargetsKey(), Settings.Get(SettingsKeys.GlobalBuildTargetsKey, new List<string> { "Clean", "Build" }));
                if (res == null || res.Count == 0)
                    return Settings.Get(SettingsKeys.GlobalBuildTargetsKey, new List<string> { "Clean", "Build" });
                return res;
            }
        }


        public string BuildPropertiesCaption => $"Build Properties ({BuildProperties.Count})...";

        public IDictionary<string, string> BuildProperties
        {
            get
            {
                Dictionary<string, string> buildProperties = new Dictionary<string, string>();
                var buildProps = Settings.Get(this.BuildPropertiesKey(), new List<BindablePair<string, string>>());
                foreach (var p in buildProps)
                {
                    if (!buildProperties.ContainsKey(p.Key))
                        buildProperties.Add(p.Key, p.Value);
                }
                return buildProperties;
            }
        }

        public PendingChange[] PendingChanges
        {
            get => pendingChanges;
            set
            {
                SetProperty(ref pendingChanges, value);
                RaisePropertyChanged(() => HasChanges);
            }
        }

        public bool HasChanges => PendingChanges != null && PendingChanges.Any();

        public Brush ImageBrush
        {
            get => imageBrush;
            set => SetProperty(ref imageBrush, value);
        }

        public WorkingFolderViewModel ParentWorkingFolder { get; }

        WorkingFolder ISolutionProjectModel.ParentWorkingFolder => ParentWorkingFolder.WorkingFolder;

        public OperationInfo CurrentOperation
        {
            get => currentOperation;
            set
            {
                if (value == Operations.Cancelling && currentOperation == Operations.None)
                    return;
                if (value == Operations.None)
                    ResetProgress();
                IsBusy = value != null && value != Operations.Paused;
                SetProperty(ref currentOperation, value);
                RaisePropertyChanged(() => IsBusy);
            }
        }

        public string ItemPath { get; }

        public string ServerItem
        {
            get
            {
                if (TfsContext.SelectedWorkspace == null)
                    return "<none>";
                return serverItem ?? (serverItem = TfsContext.SelectedWorkspace.TryGetServerItemForLocalItem(ItemPath));
            }
        }

        public string ServerItemPath => ServerItem.Replace(SolutionFileName, string.Empty);

        public Geometry ImageGeometry
        {
            get => imageGeometry;
            set => SetProperty(ref imageGeometry, value);
        }

        public bool IsIncluded
        {
            get => isIncluded;
            set
            {
                Settings.Set(this.IsIncludedKey(), value);
                SetProperty(ref isIncluded, value);
            }
        }

        public int BuildPriority
        {
            get => buildPriority;
            set
            {
                Settings.Set(this.BuildPriorityKey(), value);
                SetProperty(ref buildPriority, value);
            }
        }

        public string SolutionFileName => Path.GetFileName(ItemPath);

        public bool IsGitSourceControlled => GitHelper.IsGitControlled(ItemPath);

        public string SolutionFolder => Path.GetDirectoryName(ItemPath);

        public bool IsDelphiProject => Path.GetExtension(ItemPath).ToLower().Equals(".dproj")
                                       || Path.GetExtension(ItemPath).ToLower().Equals(".dpr");

        public object ErrorContent
        {
            get => errorContent;
            set
            {
                SetProperty(ref errorContent, value);
                ShowErrorContentCommand.RaiseCanExecuteChanged();
            }
        }

        public ImageSource IconImage => iconImage ?? (iconImage = new Bitmap(FileHelper.GetFileIconForExtensionOrFilename(ItemPath).ToBitmap().ResizeImage(20, 20)).ToImageSource());

        public void SetDefaultImageValues()
        {
            ImageGeometry = IsDelphiProject ? Pathes.Museum : Pathes.VisualStudio;
            ImageBrush = Application.Current.TryFindResource(EnvironmentColors.IconActionFillBrushKey) as Brush;
            ErrorContent = null;
        }

        public IReadOnlyCollection<Project> GetUnitTestProjects()
        {
            return unitTests ?? (unitTests = GetSolutionProjects().Where(project => project.IsTestProject()).ToList());
        }



        public IReadOnlyCollection<Project> GetSolutionProjects()
        {
            if (IsDelphiProject)
                return GetProjectsManually();

            try
            {
                if (projects == null)
                {
                    var globalProperties = new Dictionary<string, string>
                    {
                        {"SolutionDir", new FileInfo(ItemPath).DirectoryName}
                    };
                    projects = new ProjectCollection(globalProperties);
                    foreach (var p in this.ToSolution().Projects.Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat))
                    {
                        var p1 = p;
                        try
                        {
                            projects.LoadProject(Path.Combine(SolutionFolder, p1.RelativePath));
                        }
                        catch (Exception exception)
                        {
                            Output.WriteLine($"Error loading project {p1.ProjectName}: {exception}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                return GetProjectsManually();
            }

            return projects.LoadedProjects.ToList();
        }

        public ValidationResult Result
        {
            get => lastResult;
            set => SetResult(value);
        }


        public void SetResult(ValidationResult result)
        {
            lastResult = result;
            if (result.IsValid)
            {
                ImageGeometry = Pathes.Success;
                ImageBrush = Brushes.DarkGreen;
                ErrorContent = null;
            }
            else
            {
                ImageGeometry = Pathes.Error;
                ImageBrush = Brushes.DarkRed;
                ErrorContent = result.ErrorContent;
            }
        }


        public async Task OpenProjectAsync(bool forceNewInstance = false, VisualStudioInfo visualStudio = null)
        {
            if (!File.Exists(ItemPath) && !ParentWorkingFolder.IsLocal)
            {
                await GetService<CheckoutService>().CheckoutSolutionAsync(this);
            }
            await Task.Run(() =>
            {
                if ((IsDelphiProject || forceNewInstance) && File.Exists(ItemPath))
                    StartAsProcess(ItemPath, visualStudio);
                else
                    OpenInVisualStudio(ItemPath);
            });
        }

        public static void OpenInVisualStudio(string path, VisualStudioInfo visualStudio = null)
        {
            //IVsSolution vsSolution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            var service = (DTE)CheckoutAndBuild2Package.GetGlobalService<SDTE>();
            if (service != null && (!service.Solution.IsOpen || !File.Exists(service.Solution.FileName)))
            {
                service.Solution.Open(path);
                service.ExecuteCommand("View.SolutionExplorer");
            }
            else
                StartAsProcess(path, visualStudio);
        }

        public static void StartAsProcess(string path, VisualStudioInfo visualStudio = null)
        {
            if(visualStudio == null || !visualStudio.Exists) 
                Process.Start(path);
            else
            {
                Process.Start(visualStudio.ExePath, path);
            }
        }

        #region Private Methods

        private bool CanRemoveCustomSolution(object arg)
        {
            return IsManuallyAdded && ParentWorkingFolder != null && ParentWorkingFolder.Projects.Contains(this);
        }

        private void RemoveCustomSolution(object obj)
        {
            ParentWorkingFolder.RemoveCustomProject(this);
        }


        private void ExecuteSlideToOptions(PageTransition pageTransition)
        {
            if (!IsInOptionsView)
            {
                pageTransition.ShowPage(projectOptionsControl ?? (projectOptionsControl = new ProjectViewModelOptions { DataContext = this }));
                IsInOptionsView = true;
            }
            else
            {
                pageTransition.HidePage(projectOptionsControl);
                IsInOptionsView = false;
            }
        }

        private IReadOnlyCollection<Project> GetProjectsManually()
        {
            if (containingProjectsFallBack == null)
            {
                containingProjectsFallBack = new List<Project>();
                if (IsDelphiProject && !Path.GetExtension(ItemPath).ToLower().Equals(".dpr"))
                {
                    // TODO ggf Projektgruppen unterstützen
                    containingProjectsFallBack.Add(new Project(ItemPath));
                }
                else
                {
                    if (Directory.Exists(SolutionFolder))
                    {
                        foreach (var fileInfo in FileHelper.GetAllFiles(SolutionFolder, "*.csproj", "*.vbproj").OrderBy(s => Path.GetFileName(s?.FullName)))
                        {
                            var info = fileInfo;
                            Check.TryCatch<Exception>(() => containingProjectsFallBack.Add(new Project(info.FullName)));
                        }
                    }
                }
            }
            return new ReadOnlyCollection<Project>(containingProjectsFallBack);
        }


        #region PopUp and UI Handling

        private bool CanTogglePopup(Popup popup)
        {
            return canOpenPopup;
        }

        private void RegisterPopUpEvents(Popup popup)
        {
            if (!popUpEventsRegistered && popup != null)
            {
                popup.Closed += (sender, args) =>
                {
                    Task.Delay(300).ContinueWith(task =>
                    {
                        canOpenPopup = true;
                        ShowOptionsPopupCommand.RaiseCanExecuteChanged();
                    }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                    RaisePropertiesChanged(() => ServicesCaption, () => ServicesCaptionSmall);
                };
                popUpEventsRegistered = true;
            }
        }

        private void EditBuildTargets(object obj)
        {
            var window = new Window { Title = $"Specific Build Targets for {SolutionFileName}", Width = 400, Height = 400, Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.ThreeDBorderWindow };
            var listBox = new StringListEdit { DataContext = new ObservableCollection<Bindable<string>>(BuildTargets.Select(s => new Bindable<string>(s))) };
            window.Content = listBox;
            window.ShowDialog();
            var newProps = listBox.DataContext as ObservableCollection<Bindable<string>>;
            if (newProps != null)
            {
                Settings.Set(this.BuildTargetsKey(), newProps.Select(bindable => bindable.Value).ToList());
                RaisePropertiesChanged(() => BuildTargets, () => BuildTargetsCaption);
            }
        }

        private void EditBuildProperties(object obj)
        {
            var window = new Window { Title = $"Additional Build Properties for {SolutionFileName}", Width = 400, Height = 400, Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.ThreeDBorderWindow };
            var dictionaryEdit = new DictionaryEdit { DataContext = new ObservableCollection<BindablePair<string, string>>(BuildProperties.Select(pair => new BindablePair<string, string>(pair.Key, pair.Value))) };
            window.Content = dictionaryEdit;
            window.ShowDialog();
            var newProps = dictionaryEdit.DataContext as ObservableCollection<BindablePair<string, string>>;
            if (newProps != null)
            {
                Settings.Set(this.BuildPropertiesKey(), newProps.ToList());
                RaisePropertiesChanged(() => BuildProperties, () => BuildPropertiesCaption);
            }
        }

        private void TogglePopup(Popup popup)
        {
            RegisterPopUpEvents(popup);
            var openPopup = !popup.IsOpen;
            if (openPopup)
            {
                canOpenPopup = false;
                ShowOptionsPopupCommand.RaiseCanExecuteChanged();
                LoadUIComponents();
            }
            popup.IsOpen = openPopup;
        }

        private void LoadUIComponents()
        {
            if (ServiceSelectors != null && ServiceSelectors.Any())
                ServiceSelectors.Apply(box =>
                {
                    box.Checked -= ServiceBoxOnCheckChanged;
                    box.Unchecked -= ServiceBoxOnCheckChanged;
                });
            ServiceSelectors = new ObservableCollection<CheckBox>(CheckoutAndBuild2Package.GetGlobalService<MainLogic>().GetIncludedServices().Select(service => service.GetServiceSelector(this)));
            ServiceSelectors.Apply(box =>
            {
                box.Checked += ServiceBoxOnCheckChanged;
                box.Unchecked += ServiceBoxOnCheckChanged;
            });
        }

        private void ServiceBoxOnCheckChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            RaisePropertiesChanged(() => ServicesCaption, () => ServicesCaptionSmall);
        }

        #endregion

        private int GetDefaultBuildPriority()
        {
            var manager = CheckoutAndBuild2Package.GetGlobalService<IDefaultBuildPriorityManager>();
            return manager?.GetDefaultBuildPriority(this) ?? 99;
        }

        private bool CanShowErrorContent(object o)
        {
            return ErrorContent != null;
        }

        private void ShowErrorContent(object o)
        {
            var title = $"Last Error for {SolutionFileName}";
            var errorsViewModel = ErrorContent as BuildErrorsViewModel;
            if (errorsViewModel != null)
                title = errorsViewModel.Title;

            var window = new Window
            {
                Owner = Application.Current.MainWindow,
                Content = ErrorContent,
                Title = title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Height = 400,
                Width = 670
            };
            if (errorsViewModel != null)
                window.Icon = errorsViewModel.Image;
            window.Closed += (sender, args) => SetDefaultImageValues();
            window.Show();
        }

        public void ResetProgress()
        {
            if (CurrentOperation != null)
                CurrentOperation.Progress = 0;
            projectsBuildCompleted = 0;
        }

        public void IncrementProgress()
        {
            CurrentOperation?.SetProgress(GetSolutionProjects().Count + 2, ++projectsBuildCompleted);
        }

        #endregion

        private OperationInfo lastOperation;
        public void SetPaused(bool paused)
        {
            if (paused && CurrentOperation != null && CurrentOperation != Operations.Paused)
            {
                lastOperation = CurrentOperation;
                CurrentOperation = Operations.Paused;
            }
            else if (!paused && CurrentOperation == Operations.Paused && lastOperation != null)
            {
                CurrentOperation = lastOperation;
                lastOperation = null;
            }
        }
    }
}