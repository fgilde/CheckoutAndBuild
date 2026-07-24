using System;
using System.ComponentModel.Composition;
using System.Drawing;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationItems
{
	[TeamExplorerNavigationItem(GuidList.checkoutAndBuildTeamExplorerNavigationItem, 100, TargetPageId = "1F9974CD-16C3-4AEF-AED2-0CE37988E2F1")]
	public class CheckoutAndBuildTeamExplorerNavigationItem : NotificationObject, ITeamExplorerNavigationItem
	{
		#region Privates

		private readonly IServiceProvider serviceProvider;
		private Image image = Images.icon48;
		private bool isVisible = true;
		private string text = Const.ApplicationName;

		#endregion
		
		[ImportingConstructor]
		public CheckoutAndBuildTeamExplorerNavigationItem([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
		{
			this.serviceProvider = serviceProvider;
			TfsContext = serviceProvider.Get<TfsContext>();
		}

		public TfsContext TfsContext { get; private set; }

		public Image Image
		{
			get { return image; }
			set { SetProperty(ref image, value); }
		}
		
		public bool IsVisible
		{
			get { return isVisible; }
			set { SetProperty(ref isVisible, value); }
		}
		
		public string Text
		{
			get { return text; }
			set { SetProperty(ref text, value); }
		}

		public void Execute()
		{
			serviceProvider?.Get<ITeamExplorer>()?.NavigateToPage(new Guid(GuidList.checkoutAndBuildTeamExplorerMainPage), null);
		}

		public void Invalidate()
		{}

		public void Dispose()
		{}

	}
}