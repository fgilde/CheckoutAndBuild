using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.Core.WorkItems;
using CheckoutAndBuild.VisualStudio.Common;

namespace CheckoutAndBuild.VisualStudio.ViewModels
{
	/// <summary>One preview row: a work item whose text fields matched the search term.</summary>
	public class WorkItemMatchViewModel
	{
		public WorkItemMatchViewModel(WorkItemData workItem, IEnumerable<string> matchedFieldNames)
		{
			WorkItem = workItem;
			MatchedFields = string.Join(", ", matchedFieldNames);
		}

		public WorkItemData WorkItem { get; }
		public int Id => WorkItem.Id;
		public string WorkItemType => WorkItem.WorkItemType;
		public string Title => WorkItem.Title;
		public string State => WorkItem.State;
		public string MatchedFields { get; }
	}

	/// <summary>Ported WorkItemSearchReplace over Azure DevOps REST (old version used the TFS client OM).</summary>
	public class WorkItemSearchReplaceViewModel : NotificationObject
	{
		private const string organizationUrlKey = "WorkItems.OrganizationUrl";
		private const string projectKey = "WorkItems.Project";
		private const string wiqlKey = "WorkItems.Wiql";
		private const string patKey = "WorkItems.PatProtected";
		private const string defaultWiql = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project";

		private readonly ISettingsService settingsService;
		private readonly SettingsContext globalContext = new SettingsContext();

		private string organizationUrl;
		private string project;
		private string wiql;
		private string pat;
		private string searchTerm;
		private string replaceTerm;
		private string statusText = "Enter a search term...";
		private bool isBusy;
		private bool isPreviewVisible;

		private IReadOnlyList<WorkItemData> previewWorkItems;
		private Dictionary<int, List<string>> previewMatches;

		public WorkItemSearchReplaceViewModel() : this(JsonSettingsService.CreateDefault())
		{
		}

		public WorkItemSearchReplaceViewModel(ISettingsService settingsService)
		{
			this.settingsService = settingsService;
			organizationUrl = settingsService.Get(organizationUrlKey, globalContext, "");
			project = settingsService.Get(projectKey, globalContext, "");
			wiql = settingsService.Get(wiqlKey, globalContext, defaultWiql);
			pat = Unprotect(settingsService.Get(patKey, globalContext, ""));

			PreviewCommand = new DelegateCommand(async () => await PreviewAsync(), CanRun);
			ExecuteCommand = new DelegateCommand(async () => await ExecuteAsync(), () => CanRun() && isPreviewVisible);
			OpenWorkItemCommand = new DelegateCommand(OpenWorkItem);
			RunQueryCommand = new DelegateCommand(async () => await RunQueryAsync(), CanConnect);
			NewWorkItemCommand = new DelegateCommand(NewWorkItem, CanConnect);
		}

		public ObservableCollection<WorkItemMatchViewModel> Results { get; } = new ObservableCollection<WorkItemMatchViewModel>();

		public ICommand PreviewCommand { get; }
		public ICommand ExecuteCommand { get; }
		public ICommand OpenWorkItemCommand { get; }

		public string OrganizationUrl
		{
			get { return organizationUrl; }
			set { if (SetProperty(ref organizationUrl, value)) settingsService.Set(organizationUrlKey, globalContext, value); }
		}

		public string Project
		{
			get { return project; }
			set { if (SetProperty(ref project, value)) settingsService.Set(projectKey, globalContext, value); }
		}

		public string Wiql
		{
			get { return wiql; }
			set { if (SetProperty(ref wiql, value)) settingsService.Set(wiqlKey, globalContext, value); }
		}

		public string Pat
		{
			get { return pat; }
			set
			{
				if (pat == value)
					return;
				pat = value;
				settingsService.Set(patKey, globalContext, Protect(value));
			}
		}

		public string SearchTerm
		{
			get { return searchTerm; }
			set
			{
				if (SetProperty(ref searchTerm, value))
				{
					StatusText = string.IsNullOrWhiteSpace(value) ? "Enter a search term..." : "Click Preview to see the matches.";
					IsPreviewVisible = false;
				}
			}
		}

		public string ReplaceTerm
		{
			get { return replaceTerm; }
			set { SetProperty(ref replaceTerm, value); }
		}

		public string StatusText
		{
			get { return statusText; }
			set { SetProperty(ref statusText, value); }
		}

		public bool IsBusy
		{
			get { return isBusy; }
			set { SetProperty(ref isBusy, value); }
		}

		public bool IsPreviewVisible
		{
			get { return isPreviewVisible; }
			set { SetProperty(ref isPreviewVisible, value); }
		}

		private bool CanRun() => CanConnect() && !string.IsNullOrWhiteSpace(searchTerm);

		private bool CanConnect() =>
			!isBusy &&
			!string.IsNullOrWhiteSpace(organizationUrl) &&
			!string.IsNullOrWhiteSpace(project) &&
			!string.IsNullOrWhiteSpace(pat);

		#region query view (replacement for the old WorkItemsSection/dashboards)

		private const string allTypesFilter = "(All)";
		private IReadOnlyList<WorkItemData> lastQueryItems = new WorkItemData[0];
		private string selectedQueryType = allTypesFilter;

		public ObservableCollection<WorkItemData> QueryResults { get; } = new ObservableCollection<WorkItemData>();

		public ObservableCollection<string> QueryTypes { get; } = new ObservableCollection<string> { allTypesFilter };

		public ICommand RunQueryCommand { get; }
		public ICommand NewWorkItemCommand { get; }

		public string SelectedQueryType
		{
			get { return selectedQueryType; }
			set
			{
				if (SetProperty(ref selectedQueryType, value))
					ApplyQueryTypeFilter();
			}
		}

		private async System.Threading.Tasks.Task RunQueryAsync()
		{
			IsBusy = true;
			try
			{
				using (var client = CreateClient())
				{
					StatusText = "Running query...";
					IReadOnlyList<int> ids = await client.QueryIdsAsync(Wiql);
					StatusText = $"Loading {ids.Count} work items...";
					lastQueryItems = await client.GetWorkItemsAsync(ids);
				}

				string previous = selectedQueryType;
				QueryTypes.Clear();
				QueryTypes.Add(allTypesFilter);
				foreach (string type in lastQueryItems.Select(w => w.WorkItemType).Where(t => t.Length > 0).Distinct().OrderBy(t => t))
					QueryTypes.Add(type);
				selectedQueryType = QueryTypes.Contains(previous) ? previous : allTypesFilter;
				RaisePropertyChanged(nameof(SelectedQueryType));

				ApplyQueryTypeFilter();
				StatusText = $"{lastQueryItems.Count} work item(s).";
			}
			catch (Exception ex)
			{
				StatusText = ex.Message;
			}
			finally
			{
				IsBusy = false;
			}
		}

		private void ApplyQueryTypeFilter()
		{
			QueryResults.Clear();
			foreach (var item in lastQueryItems.Where(w => selectedQueryType == allTypesFilter || w.WorkItemType == selectedQueryType))
				QueryResults.Add(item);
		}

		private void NewWorkItem()
		{
			string type = selectedQueryType == allTypesFilter ? "Bug" : selectedQueryType;
			string url = $"{OrganizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(Project)}/_workitems/create/{Uri.EscapeDataString(type)}";
			System.Diagnostics.Process.Start(url);
		}

		#endregion

		private async System.Threading.Tasks.Task PreviewAsync()
		{
			IsBusy = true;
			IsPreviewVisible = false;
			Results.Clear();
			try
			{
				using (var client = CreateClient())
				{
					StatusText = "Running query...";
					IReadOnlyDictionary<string, string> textFields = await client.GetTextFieldsAsync();
					IReadOnlyList<int> ids = await client.QueryIdsAsync(Wiql);
					StatusText = $"Loading {ids.Count} work items...";
					previewWorkItems = await client.GetWorkItemsAsync(ids);
					previewMatches = WorkItemSearch.FindMatches(previewWorkItems, textFields.Keys.ToList(), SearchTerm);

					foreach (WorkItemData workItem in previewWorkItems.Where(w => previewMatches.ContainsKey(w.Id)))
						Results.Add(new WorkItemMatchViewModel(workItem,
							previewMatches[workItem.Id].Select(refName => textFields.TryGetValue(refName, out string name) ? name : refName)));
				}

				IsPreviewVisible = Results.Count > 0;
				StatusText = Results.Count > 0
					? $"{Results.Count} of {previewWorkItems.Count} work items match."
					: "No matches found.";
			}
			catch (Exception ex)
			{
				StatusText = ex.Message;
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async System.Threading.Tasks.Task ExecuteAsync()
		{
			IsBusy = true;
			try
			{
				string replaceText = ReplaceTerm ?? "";
				var matched = previewWorkItems.Where(w => previewMatches.ContainsKey(w.Id)).ToArray();
				using (var client = CreateClient())
				{
					for (int i = 0; i < matched.Length; i++)
					{
						WorkItemData workItem = matched[i];
						StatusText = $"Updating {i + 1}/{matched.Length} (#{workItem.Id})...";
						var newValues = previewMatches[workItem.Id]
							.ToDictionary(field => field, field => workItem.Fields[field].Replace(SearchTerm, replaceText));
						await client.UpdateFieldsAsync(workItem.Id, newValues);
					}
				}
				StatusText = $"Replace complete ({matched.Length} work items). You may perform a new search.";
				IsPreviewVisible = false;
				Results.Clear();
			}
			catch (Exception ex)
			{
				StatusText = ex.Message;
			}
			finally
			{
				IsBusy = false;
			}
		}

		private void OpenWorkItem(object parameter)
		{
			int id;
			switch (parameter)
			{
				case WorkItemMatchViewModel match: id = match.Id; break;
				case WorkItemData data: id = data.Id; break;
				default: return;
			}
			using (var client = CreateClient())
				System.Diagnostics.Process.Start(client.GetWorkItemUrl(id));
		}

		private WorkItemClient CreateClient() => new WorkItemClient(OrganizationUrl, Project, pat);

		private static string Protect(string value) => PatProtector.Protect(value);

		private static string Unprotect(string stored) => PatProtector.Unprotect(stored);
	}
}
