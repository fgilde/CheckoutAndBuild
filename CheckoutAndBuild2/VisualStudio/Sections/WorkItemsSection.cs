using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.WpfControls;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.workItemsSectionId, GuidList.userInfoPage, placement)]
	[TeamExplorerSectionPlacement(TeamExplorerPageIds.WorkItems, placement)]
	[TeamExplorerSectionPlacement(TeamExplorerPageIds.PendingChanges, placement)]
	[TeamExplorerSectionPlacement(TeamExplorerPageIds.GitChanges, placement)]	
	[TeamExplorerSectionPlacement(TeamExplorerPageIds.AllChanges, placement)]
    [TeamExplorerSectionPlacement(TeamExplorerPageIds.MyWork, placement)]
	public class WorkItemsSection : TeamExplorerBase, IUserContextSection, IWorkItemsListProvider
	{
		#region Private Backingfields

		private QueryDefinition2 selectedQueryDefinition;
		private const int placement = 1100;		
		private string query;
		private bool isInUserInfoPage;
		private ContextMenu createNewWorkItemMenu;
        private ContextMenu filterWorkItemTypeMenu;

        #endregion

        public IUICommand OpenWorkItemCommand { get; private set; }
		public WorkItemsListProvider WorkItemsListProvider { get; }


		public WorkItemsSection()
		{
			WorkItemsListProvider = new WorkItemsListProvider();			
			OpenWorkItemCommand = new DelegateCommand<Uri>(OpenWorkItem);
			SelectedWorkItems = new BatchedObservableCollection<WorkItemValueProvider>();		    
        }
        
	    public void OpenWorkItem(Uri uri)
		{
			TfsContext.WorkItemManager.NavigateToWorkItem(uri);
		}

		public override string Title => !string.IsNullOrEmpty(Header) ? Header : $"Workitems for {UserContext?.UserName}";

	    public string Header
		{
			get => serviceProvider.Get<SettingsService>().Get(SettingsKeys.WorkItemSectionTitleKey(UserContext),
			    $"Workitems for {UserContext?.UserName}");
	        set
			{
				serviceProvider.Get<SettingsService>().Set(SettingsKeys.WorkItemSectionTitleKey(UserContext), value);
				RaisePropertiesChanged(() => Title, () => Header);
			}
		}
		
		public QueryDefinition2 SelectedQueryDefinition
		{
			get => selectedQueryDefinition;
		    set => SetProperty(ref selectedQueryDefinition, value);
		}

		public ContextMenu CreateNewWorkItemMenu
		{
			get => createNewWorkItemMenu;
		    set => SetProperty(ref createNewWorkItemMenu, value);
		}

        public ContextMenu FilterWorkItemTypeMenu
        {
            get => filterWorkItemTypeMenu;
            set => SetProperty(ref filterWorkItemTypeMenu, value);
        }

        public bool IsInUserInfoPage
		{
			get => isInUserInfoPage;
            set => SetProperty(ref isInUserInfoPage, value);
        }


		public override object GetExtensibilityService(Type serviceType)
		{
			if (serviceType == typeof (IWorkItemsListProvider))
				return this;
			return base.GetExtensibilityService(serviceType);
		}

		public string Query
		{
			get => query;
		    set
			{
				if (SetProperty(ref query, value))
				{
					serviceProvider.Get<SettingsService>().Set(SettingsKeys.WorkItemSectionQueryKey(UserContext), value);					
					RefreshAsync();
				}
			}
		}

		public override object Content => this;

	    public UserInfoContext UserContext { get; set; }

		public bool HasItems => WorkItemsListProvider != null && WorkItemsListProvider.VisibleCount > 0;

	    public override async void Refresh()
		{
			base.Refresh();
			await RefreshAsync();
		}

		public ContextMenu ExtraContextMenu => GetExtraCommands().ToContextMenu();

	    private IEnumerable<IUICommand> GetExtraCommands()
		{
			yield return new DelegateCommand<object>("Refresh (ReRun Query)", o => RefreshAsync()) { IconImage = Images.refresh_16xLG.ToImageSource() };
			yield return new DelegateCommand<object>("View Results in Visual Studio", o => ShowQueryResults()) { IconImage = Images.ResultstoGrid_9947.ToImageSource() };
			yield return StaticCommands.Seperator;
			yield return new DelegateCommand<object>("Change Current Query...", o => SelectQueryWithDialog()) { IconImage = Images.QueryResultsNewRow.ToImageSource() };
			yield return new DelegateCommand<object>("Reset Current Query to Default", ResetQuery) { IconImage = Images.Clearallrequests_88161.ToImageSource() };
		}

		private async void ResetQuery(object obj)
		{			
			Query = TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity);
			SelectedQueryDefinition = null;
			Header = string.Empty;
			await RefreshAsync(); 
		}

		public void SelectQueryWithDialog()
		{
			QueryEditorDialog dialog = new QueryEditorDialog(Query) { CanSelectQuery = true };
			dialog.RunQuery += (o, args) => ShowQueryResults(dialog.Query);
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{				
				Header = dialog.SelectedQuery != null ? dialog.SelectedQuery.Name : "Custom Query";
                //throw new NotImplementedException("TODO: Update class changed repair FG");
				//SelectedQueryDefinition = dialog.SelectedQuery;                
				SelectedQueryDefinition = new QueryDefinition2(dialog.SelectedQuery.Name, dialog.SelectedQuery.QueryText, new QueryFolder2(dialog.SelectedQuery.Parent.Name) );
				Query = dialog.Query;								
			}
		}

		public void ShowQueryResults(string queryText)
		{
			TfsContext.WorkItemManager.ShowQueryResults(WorkItemManager.PrepareQueryText(queryText));			
		}

		public void ShowQueryResults()
		{
			if (SelectedQueryDefinition != null)
				TfsContext.WorkItemManager.ShowQueryResults(SelectedQueryDefinition);
			else
				ShowQueryResults(Query);						
		}

		/// <summary>
		/// ContextChanged event handler.
		/// </summary>
		protected override async void ContextChanged(object sender, ContextChangedEventArgs e)
		{
			base.ContextChanged(sender, e);
			if (e.TeamProjectCollectionChanged || e.TeamProjectChanged)
				await RefreshAsync();
		}

		/// <summary>
		/// Save contextual information about the current section state.
		/// </summary>
		public override void SaveContext(object sender, SectionSaveContextEventArgs e)
		{
			base.SaveContext(sender, e);
			e.Context = Query;
		}

		public override async void Initialize(object sender, IServiceProvider provider, object context)
		{			
			base.Initialize(sender, provider, context);
			IsBusy = true;
		    if (UserContext == null)
		    {
		        if(TfsContext.VersionControlServer != null)
		            UserContext = new UserInfoContext(TfsContext.VersionControlServer.AuthorizedIdentity);
		    }
			else
				IsInUserInfoPage = true;

		    if (UserContext == null)
		    {
		        IsBusy = false;
		        return;
		    }

			query = !string.IsNullOrEmpty(context?.ToString()) ? context.ToString() : serviceProvider.Get<SettingsService>().Get(SettingsKeys.WorkItemSectionQueryKey(UserContext), TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity));				
			
			TeamExplorer.PropertyChanged += TeamExplorerOnPropertyChanged;
			Check.TryCatch<Exception>(() =>
			{
				if (TfsContext?.VersionControlServer != null)
				{					
					TfsContext.VersionControlServer.AfterWorkItemsUpdated -= VersionControlServerOnAfterWorkItemsUpdated;
					TfsContext.VersionControlServer.AfterWorkItemsUpdated += VersionControlServerOnAfterWorkItemsUpdated;
				}
			});
			await RefreshAsync();
		}

		private void VersionControlServerOnAfterWorkItemsUpdated(object sender, WorkItemsUpdateEventArgs e)
		{
			Check.TryCatch<Exception>(()=>Application.Current.Dispatcher.Invoke(async () => await RefreshAsync()));			
		}

		private void TeamExplorerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (TeamExplorer != null && e.PropertyName == CoreExtensions.GetMemberName(() => TeamExplorer.CurrentPage))			
				IsInUserInfoPage = TeamExplorer.CurrentPage != null && TeamExplorer.CurrentPage.GetType() == typeof(UserInfoPage);			
		}
		
		private async Task RefreshAsync(Func<WorkItem, bool> filterFn = null )
		{
		    if (filterFn == null)
		        filterFn = item => true;
            ErrorMessage = string.Empty;
			if (!IsEnabled || TfsContext == null)
			{
				IsBusy = false;
				return;
			}
			try
			{			
				IsBusy = true;
                var workItems = (await TfsContext.WorkItemManager.RunQueryAsync(Query)).OfType<WorkItem>().Where(filterFn);
                WorkItemsListProvider.Clear();				
				UpdateMenu();			    
			    WorkItemsListProvider.AddWorkItems(workItems);
			}
			catch (Exception e)
			{
				WorkItemsListProvider.Clear();
				Output.Exception(e);
				ErrorMessage = $"Can't load Query '{Header}'" + Environment.NewLine + Environment.NewLine + e.GetMessage();
			}
			finally
			{
				IsBusy = false;
				RaisePropertyChanged(() => HasItems);
			}
		}

		private void UpdateMenu()
		{
			CreateNewWorkItemMenu = Check.TryCatch<ContextMenu, Exception>(() => TfsContext.WorkItemManager.GetWorkItemTypes().Select(type => new DelegateCommand<object>(type.Name, o => CreateNewWorkItem(type))).ToContextMenu());		    
            FilterWorkItemTypeMenu = Check.TryCatch<ContextMenu, Exception>(() => new[] { new DelegateCommand<object>("All", a => FilterByWorkItem(null)) }.Concat(TfsContext.WorkItemManager.GetWorkItemTypes().Select(type => new DelegateCommand<object>(type.Name, o => FilterByWorkItem(type)))).ToContextMenu());
        }

	    private async void FilterByWorkItem(WorkItemType type)
	    {
	        await RefreshAsync(item => type == null || item.Type == type);
	    }

	    private void CreateNewWorkItem(WorkItemType type)
		{			
			TfsContext.WorkItemManager.NavigateToWorkItem(type.NewWorkItem());
		}

		public BatchedObservableCollection<WorkItemValueProvider> SelectedWorkItems { get; } 

		public BatchedObservableCollection<WorkItemValueProvider> WorkItems => SelectedWorkItems;
	}
}