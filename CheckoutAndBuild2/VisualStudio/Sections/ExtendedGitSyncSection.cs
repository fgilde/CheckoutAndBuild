using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Git;
using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildExtendedGitSyncSection, TeamExplorerPageIds.GitCommits, 90000)]
    public class ExtendedGitSyncSection : TeamExplorerBase
	{

        public override string Title => "More";
	    public ExtendedGitSyncSection()
	    {	        
	        IsExpanded = false;
	    }


        public IEnumerable<IUICommand> DropDownCommands
	    {
	        get { yield break; }
	    }

	    public IEnumerable<IUICommand> OutgoingCommitCommands
	    {
	        get
	        {
	            yield return new DelegateCommand<object>("Force Push", ForcePush);
	        }
	    }

	    public IEnumerable<IUICommand> IncomingCommitCommands
	    {
	        get { yield break; }
	    }

        

	    public UserInfoContext UserContext { get; set; }
       

        private void ForcePush(object obj)
	    {
	        GitHelper.Push(TfsContext.SelectedDirectory, true);
            TeamExplorer.CurrentPage.Refresh();
	    }
        

		public override void Initialize(object sender, IServiceProvider provider, object context)
		{
		    IsVisible = false;
			base.Initialize(sender, provider, context);
		    if (UserContext == null)
		        UserContext = new UserInfoContext(TfsContext.VersionControlServer.AuthorizedIdentity);
        }


		public override object Content => new ItemsControl { ItemsSource = Links, Margin = new Thickness(10, 4, 0, 0) };

	    public IEnumerable<TextLink> Links => DropDownCommands.Select(command => command.ToTextLink());

       
		protected override async void OnPageChanged(ITeamExplorerPage page)
		{
			if (IsEnabled)
			{
			    await WaitForLoad();
			    if (IsCurrentPageInSectionPlacement())
			        ExtendUI();
            }
		}

	    private void ExtendSection(ITeamExplorerSection section, IEnumerable<IUICommand> commands)
	    {
	        if (section != null)
	        {
	            var sectionView = section.SectionContent as DependencyObject;
	            var link = sectionView.FindDescendant<TextLink>();
	            if (link?.Parent is WrapPanel)
	            {
	                foreach (var command in commands)
	                {
	                    ((WrapPanel)link.Parent).Children.Add(new Border { Width = 1, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.Gray, Margin = new Thickness(4, 0, 4, 0) });
	                    ((WrapPanel) link.Parent).Children.Add(command.ToTextLink());
	                }	
	            }
            }
	    }

	    private void ExtendUI()
	    {	        
	        var currentPageView = ((TeamExplorerPageBase) TeamExplorer.CurrentPage).View as UIElement;

	        var sections = TeamExplorer.CurrentPage.GetSections().ToList();
            ExtendSection(sections.FirstOrDefault(s => s.Title == "Incoming Commits"), IncomingCommitCommands);
            ExtendSection(sections.FirstOrDefault(s => s.Title == "Outgoing Commits"), OutgoingCommitCommands);

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

	}
}