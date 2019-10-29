using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.TeamFoundation.TeamExplorer.ConnectPage;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	[TeamExplorerSection(GuidList.checkoutAndBuildTeamExplorerSection, TeamExplorerPageIds.Home, 200)]
	[TeamExplorerSectionPlacement(GuidList.checkoutAndBuildTeamExplorerMainPage, 50)]
	public class CheckoutAndBuildHomeSection : TeamExplorerBase
	{
		private MainViewModel model;
		private PropertyChangedEventHandler busyChangedhandler;
		[ImportMany]
		public List<ExportFactory<IConnectPageExtendedProjectInfoProvider, IConnectPageExtendedProjectInfoProviderData>> TeamProjectExtendedInfoProviders { get; set; }

		public override void Initialize(object sender, IServiceProvider provider, object context)
		{
			base.Initialize(sender, provider, context);
			model = serviceProvider.Get<MainViewModel>();
			if (busyChangedhandler != null)
				model.PropertyChanged -= busyChangedhandler;
			busyChangedhandler = model?.OnChange(() => model.IsBusy, b => IsBusy = b);
			RegisterTeamProviders();
		}

		private void RegisterTeamProviders()
		{
			IServiceContainer service = CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>();
			if (service == null)
				return;
			service.RemoveService(typeof(List<ExportFactory<IConnectPageExtendedProjectInfoProvider, IConnectPageExtendedProjectInfoProviderData>>));					
			service.AddService(typeof(List<ExportFactory<IConnectPageExtendedProjectInfoProvider, IConnectPageExtendedProjectInfoProviderData>>), TeamProjectExtendedInfoProviders);					
		}

		#region ITeamExplorerSection

		public override void Loaded(object sender, SectionLoadedEventArgs e)
		{
		    HideSolutionSection();
            model.Update();
		}

		public override void Refresh()
		{
			model.Update();
		}

		public override object Content { get { return model; } }

		public override string Title
		{
			get { return Texts.MainSectionTitle; }
		}

		#endregion

        private void HideSolutionSection()
        {
	        //ITeamExplorerSection findSectionViewModel = TeamExplorerUtils.Instance.FindSectionViewModel(CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>(), TeamExplorerPageIds.Home, "CF14DCB8-809A-460F-B535-B2B93D5F10C");	        
			ITeamExplorer teamExplorer = serviceProvider.Get<ITeamExplorer>();
	        if (teamExplorer.CurrentPage != null)
            {
                var solutionsSection = teamExplorer.CurrentPage.GetSections()
                    .FirstOrDefault(section => section.GetType().FullName == "Microsoft.VisualStudio.TeamFoundation.TeamExplorer.Home.SolutionsSection");
				if (solutionsSection != null && serviceProvider.Get<SettingsService>().Get(SettingsKeys.HideSolutionSectionInTeamExplorerKey, true))
					SectionManager.ExcludeSection(solutionsSection);
            }
        }
	}
}