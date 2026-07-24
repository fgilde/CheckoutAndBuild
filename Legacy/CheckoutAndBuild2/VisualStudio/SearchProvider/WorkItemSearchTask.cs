using System;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace FG.CheckoutAndBuild2.VisualStudio.SearchProvider
{
	class WorkItemSearchTask : VsSearchTask
	{
		private readonly WorkItemStore store;
		private readonly IVsSearchProvider searchProvider;		

		public WorkItemSearchTask(WorkItemStore store, IVsSearchProvider provider, uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
			: base(dwCookie, pSearchQuery, pSearchCallback)
		{			
			this.store = store;
			searchProvider = provider;
		}

		/// <summary>
		/// Override to start the search
		/// </summary>
		protected override void OnStartSearch()
		{
			// Get the tokens count in the query
			uint tokenCount = this.SearchQuery.GetTokens(0, null);

			
			IVsSearchToken[] tokens = new IVsSearchToken[tokenCount];
			this.SearchQuery.GetTokens(tokenCount, tokens);

			//for (int itemIndex = 0; itemIndex < this.SearchableItems.Length; itemIndex++)
			//{
			//	SearchableItem item = this.SearchableItems[itemIndex];

			//	// Check if the search was canceled
			//	if (this.TaskStatus == VSConstants.VsSearchTaskStatus.Stopped)
			//	{
			//		// The completion was already notified by the base.OnStopSearch, there is nothing else to do
			//		return;
			//	}

			//	// Check if the item matches the current query
			//	if (Matches(item, tokens))
			//	{
			//		// Create and report new result
			//		IVsSearchProviderCallback providerCallback = (IVsSearchProviderCallback)this.SearchCallback;
			//		providerCallback.ReportResult(this, new SearchItemResult(item, this.searchProvider));

			//		// Keep track of how many results we have found, and the base class will use this number when calling the callback to report completion
			//		this.SearchResults++;
			//	}

			//	// Since we know how many items we have, we can report progress
			//	this.SearchCallback.ReportProgress(this, (uint)(itemIndex + 1), (uint)this.SearchableItems.Length);
			//}

			// Now call the base class - it will set the task status to complete and will callback to report search complete
			base.OnStartSearch();
		}

		// No need to override OnStopSearch in this case, we'll check the task status to see if the search was canceled


		//bool Matches(SearchableItem item, IVsSearchToken[] tokens)
		//{
		//	foreach (IVsSearchToken token in tokens)
		//	{
		//		bool tokenMatches = false;

		//		// We'll search description and name
		//		if (item.Name.IndexOf(token.ParsedTokenText, StringComparison.CurrentCultureIgnoreCase) != -1)
		//			tokenMatches = true;

		//		if (item.Description != null && item.Description.IndexOf(token.ParsedTokenText, StringComparison.CurrentCultureIgnoreCase) != -1)
		//			tokenMatches = true;

		//		if (!tokenMatches)
		//			return false;
		//	}

		//	return true;
		//}
	}
}