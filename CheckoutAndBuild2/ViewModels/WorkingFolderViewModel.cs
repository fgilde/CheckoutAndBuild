using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Caching;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Converter;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.VisualStudio;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.Win32;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class WorkingFolderViewModel : BaseViewModel
	{

		#region Private Backing fields

		private ContextMenu contextMenu;
		private string directory;
		private List<ProjectViewModel> projects;
		private Func<ProjectViewModel, bool> projectFilterFunc;
		private ObservableCollection<ProjectViewModel> selectedProjects;
		private bool canCheckin;
		private bool isIncludedByDefault;

		#endregion

        private Guid cacheGuid = new Guid("57FAC922-C81B-47E3-8F41-EA94D76822C5");

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public WorkingFolderViewModel(WorkingFolder workingFolder, IServiceProvider serviceProvider)
			: base(serviceProvider)
		{
			IncludeToggleCommand = new DelegateCommand<object>("Include/Exclude", ToggleSelectedProjects, o => SelectedProjects.Any()) { IconImage = Images.arrow_Reorder_16xSM.ToImageSource()};
			AddSolutionCommand = new DelegateCommand<object>("Add Project/Solution...", AddSolution) { IconImage = Images.Solution_8308.ToImageSource() };
			WorkingFolder = workingFolder;
			selectedProjects = new ObservableCollection<ProjectViewModel>();
			selectedProjects.CollectionChanged += SelectionChanged;
		}

        public bool IsExpanded
        {
            get => Settings.Get(this.IsExpandedKey(), false);
            set
            {
                Settings.Set(this.IsExpandedKey(), value);
                RaisePropertyChanged();
            }
        }


        public WorkingFolderViewModel(string directory, Func<ProjectViewModel, bool> projectFilterFunc, IServiceProvider serviceProvider)
			: this(null, serviceProvider)
		{
			Directory = directory;
			ProjectFilterFunc = projectFilterFunc;
		}

		public WorkingFolderViewModel(WorkingFolder workingFolder,Func<ProjectViewModel, bool> projectFilterFunc,
			IServiceProvider serviceProvider)
			: this(workingFolder, serviceProvider)
		{
			ProjectFilterFunc = projectFilterFunc;
		}
		

		public DelegateCommand<object> IncludeToggleCommand { get; set; }
		public WorkingFolder WorkingFolder { get; }
		public DelegateCommand<object> AddSolutionCommand { get; }

		public string SortName
		{
			get
			{
				var settings = Settings;
				return settings != null ? settings.Get(this.SortNameKey(), "Name") : "Name";
			}
			set
			{
				Settings.Set(this.SortNameKey(), value);				
				RaisePropertiesChanged(() => SortName ,() => Projects);		
			}
		}
		

		public bool IsIncludedByDefault
		{
			get => isIncludedByDefault;
		    set => SetProperty(ref isIncludedByDefault, value);
		}	

		public bool CanCheckin
		{
			get => canCheckin;
		    set => SetProperty(ref canCheckin, value);
		}

		public string Directory
		{
			get => directory;
		    set => SetProperty(ref directory, value);
		}

		public bool IsLocal => Directory != null && System.IO.Directory.Exists(Directory);

	    public bool IsGitControlled => Directory != null && GitHelper.IsGitControlled(Directory);

        public Func<ProjectViewModel, bool> ProjectFilterFunc
		{
			get => projectFilterFunc;
            set
			{
				projectFilterFunc = value;
				RaiseFilterChanged();
			}
		}

		public Func<ProjectViewModel, object> OrderKeySelectorFunc => GetOrderFunc(SortName);

	    public ObservableCollection<ProjectViewModel> SelectedProjects
		{
			get { return selectedProjects; }
			set
			{
				SetProperty(ref selectedProjects, value);
				UpdateCommands();				
			}
		}

		public ListSortDirection SortDirection
		{
			get { return Settings.Get(this.SortDirectionKey(), ListSortDirection.Ascending); }
			set
			{
				Settings.Set(this.SortDirectionKey(), value);
				RaisePropertiesChanged(() => SortDirection, () => Projects);				
			}
		}

		public bool HasProjects => Projects.Any();

	    public ProjectViewModel[] AllProjects => (projects ?? (projects = new List<ProjectViewModel>(GetContainedProjects()))).ToArray();

	    public IEnumerable<ProjectViewModel> Projects
		{
			get { return AllProjects.Where(model => ProjectFilterFunc == null || ProjectFilterFunc(model)).Order(OrderKeySelectorFunc, SortDirection); }
		}

		public string Name
		{
			get
			{
				if (IsLocal)
					return Directory;
				return $"{WorkingFolder.DisplayServerItem} ({Projects.Count()})";
			}
		}
        public ProjectViewModel AddProject(string fileName, bool raiseChanged, bool? isIncluded = null)
        {
            var projectViewModel = new ProjectViewModel(fileName, serviceProvider, this)
            {
                IsManuallyAdded = true,
                IsIncluded = isIncluded ?? IsIncludedByDefault
            };
            return AddProject(projectViewModel, raiseChanged, isIncluded);
        }


	    public ProjectViewModel AddProject(ProjectViewModel projectViewModel, bool raiseChanged, bool? isIncluded = null)
	    {
            projects.Add(projectViewModel);
            if (raiseChanged)
                RaiseFilterChanged(false);
            return projectViewModel;
        }


	    public override string ToString()
		{
			return Name;
		}


		#region ContextMenus

		public ContextMenu SortContextMenuDirections
		{
			get
			{
				return Enum.GetValues(typeof (ListSortDirection))
						.Cast<ListSortDirection>()
						.Select(d => new DelegateCommand<object>(d.ToString(), o => SortDirection = d))
						.ToContextMenu();
			}
		}

		public ContextMenu SortContextMenu
		{
			get
			{
				return (new[]
				{
					new DelegateCommand<object>("Name", o => OrderBy("Name")),
					new DelegateCommand<object>("Services", o => OrderBy("Services")),
					new DelegateCommand<object>("Build Priority", o => OrderBy("Build Priority")),
					new DelegateCommand<object>("Project Type",o => OrderBy("Project Type"))

				}).ToContextMenu();
			}
		}

		public ContextMenu WorkFolderContextMenu => GetFolderCommands().Concat(GetService<MainLogic>()
		    .GetServiceCommands(Projects)
		    .OfType<IUICommand>()
		    .Add(IsLocal ? null : StaticCommands.Seperator)
		    .Concat(GetMappingCommands()))
		    .ToContextMenu();

	    public ContextMenu ContextMenu => contextMenu ?? (contextMenu = BuildMenu());

	    #endregion

		public void RemoveCustomProject(ProjectViewModel projectViewModel, bool force = false)
		{			
			List<string> currentCustomProjects = Settings.Get(this.CustomProjectsKey(), new string[0]).ToList();
			if (projects.Contains(projectViewModel) && (force || currentCustomProjects.Contains(projectViewModel.ItemPath)))
			{
				currentCustomProjects.Remove(projectViewModel.ItemPath);
				projects.Remove(projectViewModel);
				Settings.Set(this.CustomProjectsKey(), currentCustomProjects.ToArray());
				RaiseFilterChanged(false);
			}
		}

		#region Private Methods


		private void UpdateSelectionTracking()
		{
			// Nur 4 nehmen, da es sonst derbe lahm wird
			VisualStudioTrackingSelection.UpdateSelectionTracking(SelectedProjects.Take(4).Select(model => model.OptionsObject).ToArray());
		}


		private void AddSolution(object arg)
		{
			var dlg = new OpenFileDialog {Title = "Select Project / Solutions to add" ,Multiselect = true, Filter = CheckoutAndBuild2Package.SupportedProjectExtensions.Select(s => new FileDescription(s)).GetDialogFilter() };
			if (dlg.ShowDialog() ?? false)
			{
				bool shouldSave = false;
				List<string> currentCustomProjects = Settings.Get(this.CustomProjectsKey(), new string[0]).ToList();
				foreach (string fileName in dlg.FileNames)
				{
					if (!currentCustomProjects.Contains(fileName))
					{
						AddProject(fileName, false);
						currentCustomProjects.Add(fileName);
						shouldSave = true;
					}
					else
					{
						Output.Notification($"Can't add {Path.GetFileName(fileName)} because it's already added", NotificationType.Warning);
					}
				}
				if (shouldSave)
				{
					Settings.Set(this.CustomProjectsKey(), currentCustomProjects.ToArray());
					RaiseFilterChanged(false);
				}
			}
		}

	    private ContextMenu BuildMenu()
		{
            var result = new ContextMenu();
            var commandParamBinding = new Binding {Path = CreatePropertyPath(() => SelectedProjects), Source = this};
			List<object> items = new List<object>();
			MenuItem toggleItem = IncludeToggleCommand.ToMenuItem();
			toggleItem.Icon = new Image {Width = 16, Height = 16, Source = Images.arrow_Reorder_16xSM.ToImageSource()};
			BindingOperations.SetBinding(toggleItem, HeaderedItemsControl.HeaderProperty, new Binding {Path = CreatePropertyPath(() => IncludeToggleCommand.Caption), Source = IncludeToggleCommand});
			items.Add(toggleItem);
			items.Add(StaticCommands.ShowPropertiesCommand.ToMenuItem());
			items.Add(new Separator());
			items.AddRange(serviceProvider.Get<MainLogic>().GetServiceCommands().ToMenuItems(command => $"{command.Caption} selected"));
			items.Add(new Separator());
			StaticCommands.All.OfType<DelegateCommand<IEnumerable<ProjectViewModel>>>()
				.Select(c => Tuple.Create(c, c.ToMenuItemCore()))
				.Apply(menuItem =>
				{
					BindingOperations.SetBinding((DependencyObject) menuItem.Item2, HeaderedItemsControl.HeaderProperty, new Binding {Path = CreatePropertyPath(() => menuItem.Item1.Caption), Source = menuItem.Item1});
				    var tag = menuItem.Item1.Tag as MenuItemSettings;
				    if (tag != null)
				    {
				        ((MenuItem) menuItem.Item2).FontWeight = tag.FontWeight;
				        if (tag.IsCheckInCommand)
				        {
				            BindingOperations.SetBinding((DependencyObject) menuItem.Item2, UIElement.VisibilityProperty, new Binding { Path = CreatePropertyPath(() => CanCheckin), Source = this, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Converter = new BoolToVisibilityConverter() });
				            items.Insert(0, menuItem.Item2);
				        }
				        if (tag.BindVisibility)
				        {
				            var m = (UIElement) menuItem.Item2;
				            m.Visibility = new BoolToVisibilityConverter().Convert(m.IsEnabled, typeof (Visibility), null, CultureInfo.InvariantCulture);
				            m.IsEnabledChanged += (sender, args) => ((UIElement) sender).Visibility = new BoolToVisibilityConverter().Convert(m.IsEnabled, typeof (Visibility), null, CultureInfo.InvariantCulture);				            
				        }				    
				    }
				    if(!items.Contains(menuItem.Item2))
				        items.Add(menuItem.Item2);
				});
			items.Apply(o => { BindingOperations.SetBinding((DependencyObject) o, MenuItem.CommandParameterProperty, commandParamBinding); });            
            result.Opened += (sender, args) => UpdateCommands();
            result.Opened += UpdateAsyncVisibilities;
			items.Apply(o => result.Items.Add(o));
			return result;
		}

	    private void UpdateAsyncVisibilities(object sender, RoutedEventArgs routedEventArgs)
	    {
	        var menu = sender as ContextMenu;
	        if (menu != null)
	        {
	            var selected = SelectedProjects;
                var context = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();
	            var items = menu.Items.OfType<MenuItem>().Where(item => item.Tag is MenuItemSettings);
	            foreach (var item in items.Where(item => ((MenuItemSettings)item.Tag).IsVisibleAsync != null).ToList())
	            {
	                item.Visibility = Visibility.Collapsed;	                
	                ((MenuItemSettings)item.Tag).IsVisibleAsync(selected).ContinueWith(task =>  item.Visibility = task.Result ? Visibility.Visible : Visibility.Collapsed , context);
	            }
	        }
	    }

	    private void OrderBy(string name)
		{
			SortName = name;			
		}

		private Func<ProjectViewModel, object> GetOrderFunc(string name)
		{				
			if (name == "Services")
				return model => model.ServicesCaption;
			if (name == "Build Priority")
				return model => model.BuildPriority;
			if (name == "Project Type")
				return model => Path.GetExtension(model.ItemPath);
			return model => model.SolutionFileName;
		}

		private IEnumerable<IUICommand> GetFolderCommands()
		{
			AddSolutionCommand.IconImage = Images.Solution_8308.ToImageSource();
			yield return AddSolutionCommand;
			yield return new DelegateCommand<object>("Open in File Explorer...", o => System.Diagnostics.Process.Start(IsLocal ? Directory : WorkingFolder.LocalItem)) { IconImage = Images.folder_Open_16xLG.ToImageSource() };
			yield return StaticCommands.Seperator;
		}

		private IEnumerable<IUICommand> GetMappingCommands()
		{	
			if(IsLocal)
				yield break;
			yield return new DelegateCommand<object>("Create New Mapping...", CreateNewMapping, o => HasWorkspace) { IconImage = Images.NewSolutionFolder_6289.ToImageSource() };
			yield return StaticCommands.Seperator;
			yield return new DelegateCommand<object>("Edit Mapping...", EditMapping, o=>HasWorkspace) { IconImage = Images.Folder_special_open__5844_16x.ToImageSource() };
            yield return new DelegateCommand<object>("Delete Mapping...", DeleteMapping,o=>HasWorkspace) { IconImage = Images.FolderOffline_7441.ToImageSource() };
		}



		private void DeleteMapping(object obj)
		{
			Output.Notification("Are you sure to delete this Workingfolder mapping  [Delete Workingfolder Mapping](Click to remove mapping).",
				NotificationType.Warning, NotificationFlags.None, new DelegateCommand<object>(param =>
				{					
					Output.HideNotification(GuidList.deleteMappingNotificationId.ToGuid());
					TfsContext.SelectedWorkspace.DeleteMapping(WorkingFolder);
					serviceProvider.Get<MainViewModel>().Update();
				}, param => true), GuidList.deleteMappingNotificationId.ToGuid());
		}

		private void CreateNewMapping(object obj)
		{
			if (CreateMappingDialog.CreateNewMapping(TfsContext.SelectedWorkspace, TfsContext))
				serviceProvider.Get<MainViewModel>().Update();
		}

		private void EditMapping(object obj)
		{
			if(CreateMappingDialog.ChangeMapping(TfsContext.SelectedWorkspace, TfsContext, WorkingFolder))
				serviceProvider.Get<MainViewModel>().Update();
		}

		private PropertyPath CreatePropertyPath<T>(Expression<Func<T>> expr)
		{
			return new PropertyPath(expr.GetMemberName());
		}

	    public string FileCacheKey => $"{Directory}_{cacheGuid}_{GitHelper.GetCurrentBranchName(Directory)}";

	    public WorkingFolderViewModel ResetFileCache()
	    {
	        cacheGuid = Guid.NewGuid();
	        return this;
	    }

		private ProjectViewModel[] GetContainedProjects()
		{
			string[] supportedExtensions = CheckoutAndBuild2Package.SupportedProjectExtensions.Select(s => "*" + s).ToArray();
			ProjectViewModel[] result = null;
			if (IsConnected && HasWorkspaceOrDirectory && !IsLocal)
			{
				var tfsContext = TfsContext;
				result = WorkingFolder.GetFiles(tfsContext.SelectedWorkspace, supportedExtensions).Where(s => !string.IsNullOrEmpty(s))
					.Select(s => new ProjectViewModel(s, serviceProvider, this)).ToArray();
			}
			if (IsLocal)
			{
			    if (IsGitControlled)
			    {
			        result = GitHelper.GetAllFiles(Directory, supportedExtensions).Where(s => s.Exists)
			            .Select(s => new ProjectViewModel(s.FullName, serviceProvider, this)).ToArray();
                }
			    else
			    {
			        result = MemoryCache.Default.AddOrGetExisting(FileCacheKey, () =>
			        {
			            return FileHelper.GetAllFiles(Directory, supportedExtensions).Where(s => s.Exists)
			                .Select(s => new ProjectViewModel(s.FullName, serviceProvider, this)).ToArray();
			        });
			    }
			}
			if (result != null)
			{
				return result.Concat(GetCustomProjects()).ToArray();				
			}

			return Enumerable.Empty<ProjectViewModel>().ToArray();
		}

		private ProjectViewModel[] GetCustomProjects()
		{
			return Settings.Get(this.CustomProjectsKey(), new string[0]).Distinct().Where(File.Exists).Select(s => new ProjectViewModel(s, serviceProvider, this) {IsManuallyAdded = true}).ToArray();
		}

		private void ToggleSelectedProjects(object o)
		{
			foreach (ProjectViewModel project in SelectedProjects)
				project.IsIncluded = !project.IsIncluded;
			RaiseFilterChanged();
		}

		private void SelectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
		{
			UpdateCommands();
			UpdateSelectionTracking();
		}

		private void UpdateCommands()
		{
			IncludeToggleCommand.RaiseCanExecuteChanged();
			IncludeToggleCommand.Caption = SelectedProjects.Count > 0
				? (SelectedProjects.Any(model => !model.IsIncluded) ? "Include selected" : "Excluded selected")
				: "Include/Exclude";
			StaticCommands.RaiseAllCanExecuteChanged();
			serviceProvider.Get<MainLogic>().GetServiceCommands().Apply(command => command.RaiseCanExecuteChanged());
			CanCheckin = SelectedProjects.Any(model => model.HasChanges);
		}

		internal void RaiseFilterChanged(bool refreshAllProjects = true)
		{
			if (refreshAllProjects)
				projects = new List<ProjectViewModel>(GetContainedProjects());
			RaisePropertiesChanged(() => AllProjects, () => Projects, () => Name, () => HasProjects);
		}

		#endregion

	}
}