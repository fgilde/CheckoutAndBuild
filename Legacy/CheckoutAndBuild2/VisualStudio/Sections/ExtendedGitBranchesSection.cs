using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Controls.Forms;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildExtendedGitBranchSection, TeamExplorerPageIds.GitBranches, 11)]
	public class ExtendedGitBranchesSection : TeamExplorerBase
	{

	    private UIElement currentPageView;
	    private TextBox textBoxBranchName;
        public override string Title => "More";

		public DelegateCommand<object> BranchSuggestionCommand { get; }

	    public UserInfoContext UserContext { get; set; }

        public IEnumerable<IUICommand> DropDownCommands
	    {
	        get
	        {
	            yield return BranchSuggestionCommand;
	        }
        }

	    public IEnumerable<IUICommand> WorkItemContextMenuCommands
	    {
	        get
	        {
	            yield return new DelegateCommand<object>("Change Query", ChangeWorkItemsQuery);
	            yield return new DelegateCommand<object>("Enter WorkitemID", EnterWorkItemId);
	            yield return new SeparatorCommand();
	        }
	    }

	    private void ChangeWorkItemsQuery(object obj)
	    {
	        var query = serviceProvider.Get<SettingsService>().Get(SettingsKeys.WorkItemSectionQueryKey(UserContext), TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity));
	        if (string.IsNullOrEmpty(query))
	            query = TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity);
            QueryEditorDialog dialog = new QueryEditorDialog(query) { CanSelectQuery = true };
	        dialog.RunQuery += (o, args) => TfsContext.WorkItemManager.ShowQueryResults(WorkItemManager.PrepareQueryText(dialog.Query));
	        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
	            serviceProvider.Get<SettingsService>().Set(SettingsKeys.WorkItemSectionQueryKey(UserContext), dialog.Query);
        }

	    private void EnterWorkItemId(object obj)
	    {
	        if (int.TryParse(Prompt.ShowDialog("Enter WorkitemId", "Workitem Id"), out int id))
	        {
	            var workItem = Check.TryCatch<WorkItem, Exception>(() => TfsContext.WorkItemManager.WorkItemStore.GetWorkItem(id));
	            if (workItem != null)
	                MakeNameSuggestionForWorkItem(workItem);
	            else
	                MessageBox.Show($"No workitem with Id ({id}) found");
	        }
	    }

	    public ExtendedGitBranchesSection()
		{
			BranchSuggestionCommand = new DelegateCommand<object>("Branch Suggestion", OnBranchSuggestionClick, CanMakeSuggestions);			
			IsExpanded = false;
		}

		public override void Initialize(object sender, IServiceProvider provider, object context)
		{
		    IsVisible = false;
			base.Initialize(sender, provider, context);
		    if (UserContext == null && TfsContext.VersionControlServer != null)
		        UserContext = new UserInfoContext(TfsContext.VersionControlServer.AuthorizedIdentity);
		    if (context is WorkItem item)
		    {
		        OpenCreateBranchEdit();
                MakeNameSuggestionForWorkItem(item);
		    }
        }

	    private void OpenCreateBranchEdit()
	    {
	        
	    }


	    public override object Content => new ItemsControl { ItemsSource = Links, Margin = new Thickness(10, 4, 0, 0) };

	    public IEnumerable<TextLink> Links => DropDownCommands.Select(command => command.ToTextLink());

       
	    private async void OnBranchSuggestionClick(object o)
	    {
	        (await GetWorkItemContextMenu()).IsOpen = true;
	    }


        private bool CanMakeSuggestions(object arg)
		{
		    return true;
		}

		private void UpdateCanExecute()
		{			
			BranchSuggestionCommand.RaiseCanExecuteChanged();
		}


		protected override async void OnPageChanged(ITeamExplorerPage page)
		{
		    page?.OnChange(() => page.IsBusy, b => UpdateCanExecute(), true);
		    UpdateCanExecute();
			if (IsEnabled)
			{
			    await WaitForLoad();
                if (IsCurrentPageInSectionPlacement())
			        ExtendUI();			    
			}
		}

	    private async Task<IEnumerable<WorkItem>> GetWorkItems()
	    {	      
			if (!TfsContext.IsTfsConnected)
	            return Enumerable.Empty<WorkItem>();  
	        var query = serviceProvider.Get<SettingsService>().Get(SettingsKeys.WorkItemSectionQueryKey(UserContext), TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity));
	        if (string.IsNullOrEmpty(query))
	            query = TfsContext.WorkItemManager.GetDefaultUserWorkItemQuery(UserContext.Identity);
            return (await TfsContext.WorkItemManager.RunQueryAsync(query)).OfType<WorkItem>();            
        }

	    private async Task<ContextMenu> GetWorkItemContextMenu()
	    {
	        var commands = WorkItemContextMenuCommands.Concat((await GetWorkItems()).Select(item => new DelegateCommand<WorkItem>($"{item.Id} - ({item.Type.Name}) {item.Title}", MakeNameSuggestionForWorkItem)
	        {
	            Parameter = item
	        }));
            return commands.ToContextMenu();
	    }

	    private void MakeNameSuggestionForWorkItem(WorkItem workItem)
	    {	        
            if(textBoxBranchName != null)
	            textBoxBranchName.Text = GetNameSuggestion(workItem);
            else
                Clipboard.SetText(GetNameSuggestion(workItem));
	    }

	    private string GetNameSuggestion(WorkItem workItem)
	    {         
            var sourceName = Check.TryCatch<string, Exception>(() => currentPageView.FindDescendants<ComboBox>().ToArray()[1].Text);
	        return GetNameSuggestion(workItem, TfsContext.SelectedDirectory, sourceName);
	    }

	    public static string GetNameSuggestion(WorkItem workItem, string gitDir, string sourceName = "")
        {
	        if (string.IsNullOrEmpty(sourceName))
	            sourceName = GitHelper.GetCurrentBranch(gitDir) ?? "";

            string prefix = "wip/";

            if (sourceName.StartsWith(prefix))
                sourceName = sourceName.Substring(0, sourceName.LastIndexOf("/", StringComparison.OrdinalIgnoreCase));
            else if (sourceName.Contains("/"))
	            sourceName = sourceName.Split('/').LastOrDefault() ?? "";

	        string typeName = workItem.Type.Name.ToLower();
            typeName = typeName.Replace(" ", "");

            if (sourceName.StartsWith(prefix))
            {
                if (!sourceName.EndsWith("/"))
                    sourceName += "/";

                return $"{sourceName}{typeName}-{workItem.Id}-{workItem.Title.Replace(" ", "")}";
            }

            if (sourceName == "develop" || sourceName == "master")
            {
                prefix = sourceName == "develop" ? "bugfix/" : "hotfix/";
                sourceName = "";
            }

            if (!string.IsNullOrEmpty(sourceName) && !sourceName.EndsWith("/"))
                sourceName += "/";

            return $"{prefix}{sourceName}{typeName}-{workItem.Id}-{workItem.Title.Replace(" ", "")}";
        }

        private async void ExtendUI()
	    {	        
	        currentPageView = ((TeamExplorerPageBase) TeamExplorer.CurrentPage).View as UIElement;

	        await ExtendNewBranchNameTextBox();

	        var dropDownLink = currentPageView.FindDescendant<DropDownLink>(link => link.Text == "Actions");
	        if (dropDownLink?.Parent is WrapPanel && DropDownCommands.Any())
	        {
	            ((WrapPanel)dropDownLink.Parent).Children.Add(new Border { Width = 1, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.Gray, Margin = new Thickness(4, 0, 0, 0) });
                ((WrapPanel) dropDownLink.Parent).Children.Add(new DropDownLink
	            {
	                Tag = "PE",
	                Text = Title,
                    Margin = new Thickness(5,0,0,0),
	                DropDownMenu = DropDownCommands.ToContextMenu()
	            });
	        }
	        else
	        {
	            IsVisible = Links.Any();
	        }
	    }

	    private async Task ExtendNewBranchNameTextBox()
	    {
	        textBoxBranchName = currentPageView.FindDescendant<TextBox>();
	        if (textBoxBranchName != null)
	            textBoxBranchName.ContextMenu = await GetWorkItemContextMenu();
	        
	    }
	}
}