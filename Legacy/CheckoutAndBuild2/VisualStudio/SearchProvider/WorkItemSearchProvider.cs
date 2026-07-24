using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Shell.Interop;

namespace FG.CheckoutAndBuild2.VisualStudio.SearchProvider
{
	[Guid(GuidList.workItemSearchProvider)]
	public class WorkItemSearchProvider : IVsSearchProvider
	{
		internal T Get<T>() where T : class
		{
			return CheckoutAndBuild2Package.GetGlobalService<T>();
		}

		public IVsSearchItemResult CreateItemResult(string lpszPersistenceData)
		{
			return WorkItemSearchResult.CreateItemResult(lpszPersistenceData, new WorkItem[0], this);			
		}

		public IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchProviderCallback pSearchCallback)
		{
			return new WorkItemSearchTask(Get<TfsContext>().WorkItemStore, this, dwCookie, pSearchQuery, pSearchCallback);
		}

		/// <summary>
		/// Injects specialized settings into the data model associated with the command search provider.
		/// </summary>
		/// <param name="pSearchOptions">[in] The data model into which to place any special values to control how the Quick Access service treats this search provider.</param>
		public void ProvideSearchSettings(IVsUIDataSource pSearchOptions)
		{}


		/// <summary>
		/// Gets a displayable name for the search provider, for example "Menu items."
		/// </summary>
		/// <returns>
		/// The name for the search provider.
		/// </returns>
		public string DisplayText => "Workitem search";

	    /// <summary>
		/// Gets a description of the provider results, for example "Searches top-level menu items."
		/// </summary>
		/// <returns>
		/// The description of the provider results.
		/// </returns>
		public string Description => "A Quick Launch search provider for workitems";

	    /// <summary>
		/// Gets a tooltip for the provider. The tooltip is displayed when it appears in the Global Search UI instead of "Show results from this category only".
		/// </summary>
		/// <returns>
		/// The tooltip for the provider.
		/// </returns>
		public string Tooltip => string.Empty;

	    /// <summary>
		/// Gets an identifier for the search provider.
		/// </summary>
		/// <returns>
		/// The search provider identifier.
		/// </returns>
		public Guid Category => new Guid(GuidList.workItemSearchProvider);

	    /// <summary>
		/// Gets a unique category shortcut that can be used in filtering the results from multiple providers. For example, searching for "@cmd" only returns search results from the provider with category shortcut "cmd".
		/// </summary>
		/// <returns>
		/// The unique category shortcut.
		/// </returns>
		public string Shortcut => "Workitems";
	}

}