using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class WorkingFolderListViewModel : BaseViewModel
	{
		private ObservableCollection<WorkingFolderViewModel> workingFolders;
		private string title;
		private readonly Dictionary<PropertyChangedEventHandler, WorkingFolderViewModel> eventHandlers = new Dictionary<PropertyChangedEventHandler, WorkingFolderViewModel>();

		public event EventHandler<EventArgs<IEnumerable<ProjectViewModel>>> ProjectsChanged;

		public string Title
		{
			get { return title; }
			set { SetProperty(ref title, value); }
		}

		public WorkingFolderListViewModel(IServiceProvider serviceProvider)
			: this(Enumerable.Empty<WorkingFolderViewModel>(), serviceProvider)
		{ }

		public WorkingFolderListViewModel(IEnumerable<WorkingFolderViewModel> workingFolders, IServiceProvider serviceProvider) : base(serviceProvider)
		{
			WorkingFolders = new ObservableCollection<WorkingFolderViewModel>(workingFolders);			
		}
		
		public ObservableCollection<WorkingFolderViewModel> WorkingFolders
		{
			get { return workingFolders; }
			set { SetProperty(ref workingFolders, value); }
		}

        public bool IsExpanded
        {
            get { return Settings.Get(this.IsExpandedKey(), false); }
            set
            {
                Settings.Set(this.IsExpandedKey(), value);
                RaisePropertyChanged();
            }
        }

        public bool HasProjects => ProjectCount > 0;

	    public int ProjectCount
		{
			get { return WorkingFolders.SelectMany(model => model.Projects).Count(); }
		}


		public void UpdateCollection(IEnumerable<WorkingFolderViewModel> folders)
		{
			//WorkingFolders.UpdateCollection(folders);
			IEnumerable<WorkingFolderViewModel> workingFolderViewModels = folders as WorkingFolderViewModel[] ?? folders.ToArray();			
			if (WorkingFolders != null && WorkingFolders.Any())
			{
				foreach (var model in WorkingFolders)
					DetachHandlers(model);
			}

			WorkingFolders = new ObservableCollection<WorkingFolderViewModel>(workingFolderViewModels);
			foreach (var folderViewModel in workingFolderViewModels)
			{
				WorkingFolderViewModel model = folderViewModel;
				eventHandlers.Add(folderViewModel?.OnChange(() => model.Projects, RaiseContainedProjectsChanged ), model);
			}
			RaisePropertiesChanged(() => ProjectCount, () => HasProjects);
		}

		private void DetachHandlers(WorkingFolderViewModel model)
		{
			foreach (KeyValuePair<PropertyChangedEventHandler, WorkingFolderViewModel> handler in eventHandlers.Where(pair => pair.Value == model).ToList())
			{
				handler.Value.PropertyChanged -= handler.Key;
				eventHandlers.Remove(handler.Key);
			}
			
		}

		private void RaiseContainedProjectsChanged(IEnumerable<ProjectViewModel> models)
		{
			EventHandler<EventArgs<IEnumerable<ProjectViewModel>>> handler = ProjectsChanged;
			if (handler != null) handler(this, new EventArgs<IEnumerable<ProjectViewModel>>(models));
		}
	}
}