using System;
using FG.CheckoutAndBuild2.Properties;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
	[TeamExplorerPage(GuidList.checkoutAndBuildTeamExplorerMainPage, Undockable = true, MultiInstances = false)]
	public class CheckoutAndBuildTeamExplorerMainPage : TeamExplorerBase
	{
		public override string Title => Const.ApplicationName;
	}
}
