using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.WpfControls;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class WorkItemSearchReplaceViewModel : BaseViewModel
    {
		#region Private Backingfields

		private readonly QueryItem queryItem;
		private readonly ITeamFoundationContext context;
		private string queryName;
		private int _queryWorkItemCount;
		private string searchTerm;
		private string replaceTerm;
		private bool isPreviewVisible;
		private string statusText;
		private Dictionary<int, List<string>> workItemFieldMap;
		private HashSet<string> fieldMatches;

		#endregion

		public WorkItemSearchReplaceViewModel(QueryItem queryItem, IServiceProvider provider)
			: base(provider)
        {
            this.queryItem = queryItem;
			context = TfsContext.TfsContextManager.CurrentContext;

			PreviewCommand = new DelegateCommand<object>(Preview, CanPreview);
			ExecuteCommand = new DelegateCommand<object>(Execute, CanExecute);
			OpenWorkItemCommand = new DelegateCommand<Uri>(OpenWorkItem);


			WorkItemsListProvider = new WorkItemsListProvider();
            PreviewFields = new ObservableCollection<string>();
			Initialize();
        }

		public void OpenWorkItem(Uri uri)
		{
			TfsContext.WorkItemManager.NavigateToWorkItem(uri);
		}

        public void Initialize()
        {
            StatusText = "Enter a search term...";
            QueryName = queryItem.Name;
            UpdateQueryCount();
        }

        private async void UpdateQueryCount()
        {            
	        using (new PauseCheckedActionScope(() => IsBusy = true, () => IsBusy = false))
	        {
				await Task.Run(() =>
				{
					var query = TfsContext.WorkItemStore.GetQueryDefinition(queryItem.Id);
					QueryWorkItemCount = TfsContext.WorkItemStore.QueryCount(query.QueryText, TfsContext.WorkItemManager.Context);
				});
	        }
        }

		public IUICommand OpenWorkItemCommand { get; private set; }
		public IUICommand PreviewCommand { get; private set; }
		public IUICommand ExecuteCommand { get; private set; }

        public async void Preview(object parameter)
        {
            IsBusy = true;
            IsPreviewVisible = false;
            WorkItemsListProvider.Clear();
            PreviewFields.Clear();
			BatchedObservableCollection<WorkItem> foundWorkItems = new BatchedObservableCollection<WorkItem>();

			await Task.Run(() =>
            {
				QueryDefinition query = TfsContext.WorkItemStore.GetQueryDefinition(queryItem.Id);
				WorkItemCollection workItems = TfsContext.WorkItemStore.Query(query.QueryText, TfsContext.WorkItemManager.Context);
                
                workItemFieldMap = new Dictionary<int, List<string>>();
                fieldMatches = new HashSet<string>();
                
                foreach (WorkItem workItem in workItems)
                {
                    bool matchedCurrent = false;
                    foreach (Field field in workItem.Fields.Cast<Field>().Where(f => IsStringField(f.FieldDefinition)))
                    {
                        if (field.Value.ToString().Contains(SearchTerm))
                        {
                            if (!matchedCurrent)
                            {
								foundWorkItems.Add(workItem);
                                matchedCurrent = true;
                                workItemFieldMap.Add(workItem.Id, new List<string>());
                            }

                            fieldMatches.Add(field.Name);
                            workItemFieldMap[workItem.Id].Add(field.Name);
                        }
                    }
                }
            });
			
			WorkItemsListProvider.AddWorkItems(foundWorkItems);
			PreviewFields.AddRange(fieldMatches);

			bool matchFound = WorkItemsListProvider.VisibleCount > 0;

            StatusText = matchFound? "" : "No matches found.";
            IsPreviewVisible = matchFound;
            IsBusy = false;
        }

        private static bool IsStringField(FieldDefinition fieldDef)
        {
            return 
                fieldDef.FieldType == FieldType.Html ||
                fieldDef.FieldType == FieldType.PlainText ||
                fieldDef.FieldType == FieldType.String;
        }

        public bool CanPreview(object parameter)
        {
            return !IsBusy && !String.IsNullOrWhiteSpace(this.SearchTerm);
        }

        public async void Execute(object parameter)
        {
            IsBusy = true;
            await Task.Run(() =>
            {
                string replaceText = ReplaceTerm ?? "";

				WorkItem[] workItems = WorkItemsListProvider.WorkItems.AsWorkItems().ToArray();
				foreach (var workItem in workItems)
                {
                    workItem.Open();

                    foreach (var fieldName in workItemFieldMap[workItem.Id])
                    {
                        var field = workItem.Fields[fieldName];
                        var original = field.Value.ToString();
						var replaced = original.Replace(SearchTerm, replaceText);

                        field.Value = replaced;
                    }
                }

                var store = context.TeamProjectCollection.GetService<WorkItemStore>();
				store.BatchSave(workItems);
            });
            StatusText = "Replace complete. You may perform a new search.";
            IsPreviewVisible = false;
	        WorkItemsListProvider.Clear();
            PreviewFields.Clear();
            IsBusy = false;
        }

		public WorkItemsListProvider WorkItemsListProvider { get; private set; }

        public bool CanExecute(object parameter)
        {
            return !IsBusy && !String.IsNullOrWhiteSpace(SearchTerm); 
        }
        
        public ObservableCollection<string> PreviewFields { get; private set; }
		
		public string StatusText
		{
			get { return statusText; }
			set { SetProperty(ref statusText, value); }
		}
		
		public bool IsPreviewVisible
		{
			get { return isPreviewVisible; }
			set { SetProperty(ref isPreviewVisible, value); }
		}
		
		public string QueryName
		{
			get { return queryName; }
			set { SetProperty(ref queryName, value); }
		}

		public int QueryWorkItemCount
		{
			get { return _queryWorkItemCount; }
			set { SetProperty(ref _queryWorkItemCount, value); }
		}

        public string SearchTerm
        {
            get { return searchTerm; }
            set
            {
	            if (SetProperty(ref searchTerm, value))
	            {
		            StatusText = String.IsNullOrWhiteSpace(searchTerm) ? "Please enter a search term..." : "Click Preview to see the results of the match.";
		            IsPreviewVisible = false;		            
		            RaiseCanExecuteChanged();
	            }
            }
        }

		public string ReplaceTerm
		{
			get { return replaceTerm; }
			set { SetProperty(ref replaceTerm, value); }
		}

        
        private void RaiseCanExecuteChanged()
        {
            ExecuteCommand.RaiseCanExecuteChanged();
            PreviewCommand.RaiseCanExecuteChanged();
        }
    }
}
