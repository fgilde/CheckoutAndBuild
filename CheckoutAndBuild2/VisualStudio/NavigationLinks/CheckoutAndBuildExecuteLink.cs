using System;
using System.ComponentModel.Composition;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationLinks
{

	[TeamExplorerNavigationLink(GuidList.checkoutAndBuildSettingsLinkId, GuidList.checkoutAndBuildTeamExplorerNavigationItem, 100)]
	public class CheckoutAndBuildExecuteLink : BaseViewModel, ITeamExplorerNavigationLink
	{

		/// <summary>
		/// Constructor.
		/// </summary>
		[ImportingConstructor]
		public CheckoutAndBuildExecuteLink([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) : 
			base(serviceProvider)	
		{}


		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{}

		/// <summary>
		/// Execute this link.
		/// </summary>
		public void Execute()
		{
			serviceProvider.Get<MainViewModel>().CheckoutAndBuildMainCommand.Execute(null);
		}

		/// <summary>
		/// Invalidate the state of this item.
		/// </summary>
		public void Invalidate()
		{}

		/// <summary>
		/// Get the text of this link. If the text changes, the PropertyChanged event should be raised.
		/// </summary>
		/// <returns>
		/// Returns <see cref="T:System.String"/>.
		/// </returns>
		public string Text { get { return string.Format("Execute {0}", Const.ApplicationName); } }

		/// <summary>
		/// Get the enabled state of this link. If the state changes, the PropertyChanged event should be raised.
		/// </summary>
		/// <returns>
		/// Returns <see cref="T:System.Boolean"/>.
		/// </returns>
		public bool IsEnabled { get {return IsConnected && HasWorkspaceOrDirectory; }}

		/// <summary>
		/// Get the visibility of this link. If the visibility changes, the PropertyChanged event should be raised.
		/// </summary>
		/// <returns>
		/// Returns <see cref="T:System.Boolean"/>.
		/// </returns>
		public bool IsVisible { get { return true; }}
	}
}