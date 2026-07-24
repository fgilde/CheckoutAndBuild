using System;
using System.ComponentModel.Composition;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationLinks
{

	[TeamExplorerNavigationLink(GuidList.checkoutAndBuildSettingsLinkId, GuidList.checkoutAndBuildTeamExplorerNavigationItem, 200)]
	public class CheckoutAndBuildSettingsLink : SimplePageNavigationLink
	{
		public override string Text { get { return string.Format("{0} Settings", Const.ApplicationName); } }

		[ImportingConstructor]
		public CheckoutAndBuildSettingsLink([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider): 
			base(serviceProvider)	
		{}

		public override void Execute()
		{
			serviceProvider.Get<SettingsService>().ShowMainSettingsPage();
		}
	}
}