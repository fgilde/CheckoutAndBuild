using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.Win32;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildExtendedGitCheckinSection, TeamExplorerPageIds.GitChanges, 11)]
	public class ExtendedGitChangesSection : TeamExplorerBase
	{		

		public override string Title => "Git Utils";

		public DelegateCommand<object> CreateGitPatchCommand { get; }
		public DelegateCommand<object> ApplyGitPatchCommand { get; }

	    public IEnumerable<IUICommand> Commands
	    {
	        get
	        {
	            yield return CreateGitPatchCommand;
	            yield return ApplyGitPatchCommand;
	        }
        }

		public ExtendedGitChangesSection()
		{
			CreateGitPatchCommand = new DelegateCommand<object>("Create Git patch file...", CreateGitPatch, CanCreateGitPatch);			
			ApplyGitPatchCommand = new DelegateCommand<object>("Apply a Git patch file...", ApplyGitPatch, CanApplyGitPatch);			
			IsExpanded = false;
		}

	    private bool CanApplyGitPatch(object arg)
	    {
	        //return !GitHelper.HasChanges(TfsContext.SelectedDirectory);
	        return true;
        }

	    private bool CanCreateGitPatch(object arg)
	    {
	        //return GitHelper.HasChanges(TfsContext.SelectedDirectory);
	        return true;
	    }

        private void ApplyGitPatch(object obj)
	    {
	        var dialog = new OpenFileDialog { Filter = "Patch File | *.patch", DefaultExt = "patch", FileName = $"{GitHelper.GetCurrentBranchName(TfsContext.SelectedDirectory)}.patch" };
	        if (dialog.ShowDialog() ?? false)
	        {
	            GitHelper.ApplyGitPatch(TfsContext.SelectedDirectory, dialog.FileName);
                TeamExplorer.CurrentPage.Refresh();
	        }
        }

	    private void CreateGitPatch(object o)
		{            		    
		    var dialog = new SaveFileDialog { Filter = "Patch File | *.patch", DefaultExt = "patch", FileName = $"{GitHelper.GetCurrentBranchName(TfsContext.SelectedDirectory)}.patch"};
		    if (dialog.ShowDialog() ?? false)
		    {
		        GitHelper.CreateGitPatch(TfsContext.SelectedDirectory, dialog.FileName, true);
		    }
		}

		

		public override void Initialize(object sender, IServiceProvider provider, object context)
		{
		    IsVisible = false;
			base.Initialize(sender, provider, context);
		}


		public override object Content => new ItemsControl { ItemsSource = Links, Margin = new Thickness(10, 4, 0, 0) };

	    public IEnumerable<TextLink> Links => Commands.Select(command => command.ToTextLink());
		

		//private Task<PendingChange[]> GetChangesToUndoAsync(bool included)
		//{
		//	return Task.Run(() =>
		//	{
		//		var vs = serviceProvider.Get<VersionControlExt>();
		//		if (included)
		//		{
		//			if (vs.PendingChanges.IncludedChanges.Any())
		//				return vs.PendingChanges.IncludedChanges.Where(change => !change.HasReallyChange()).ToArray();
		//		}
		//		else
		//		{
		//			if (vs.PendingChanges.ExcludedChanges.Any())
		//				return vs.PendingChanges.ExcludedChanges.Where(change => !change.HasReallyChange()).ToArray();
		//		}
		//		return new PendingChange[0];
		//	});
		//}

		
		private void UpdateCanExecute()
		{			
			CreateGitPatchCommand.RaiseCanExecuteChanged();
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

	    private void ExtendUI()
	    {	        
	        var view = ((TeamExplorerPageBase) TeamExplorer.CurrentPage).View as UIElement;
	        var dropDownLink = view.FindDescendant<DropDownLink>(link => link.Text == "Actions");
	        if (dropDownLink?.Parent is WrapPanel)
	        {
	            ((WrapPanel) dropDownLink.Parent).Children.Add(new DropDownLink
	            {
	                Tag = "PE",
	                Text = "More",
                    Margin = new Thickness(5,0,0,0),
	                DropDownMenu = Commands.ToContextMenu()
	            });
	        }
	        else
	        {
	            IsVisible = true;
	        }
	    }
	}
}