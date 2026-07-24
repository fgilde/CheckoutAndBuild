using System;
using System.Windows.Forms;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Shell.Interop;
using OLEInterop = Microsoft.VisualStudio.OLE.Interop;

namespace FG.CheckoutAndBuild2.VisualStudio.SearchProvider
{
	public class WorkItemSearchResult : IVsSearchItemResult
	{
		readonly WorkItem item;
		readonly IVsSearchProvider provider;

		public WorkItemSearchResult(WorkItem item, IVsSearchProvider provider)
		{
			this.item = item;
			this.provider = provider;
		}

		#region IVsSearchItemResult

		public OLEInterop.IDataObject DataObject
		{
			get { return null; }
		}

		public string Description
		{
			get { return item.Description; }
		}

		public string DisplayText
		{
			get { return item.NodeName; }
		}

		public IVsUIObject Icon
		{
			get
			{				
				
				return null;

				// If all items returned from the search provider use the same icon, consider using a static member variable 
				// (e.g. on the search provider class) to initialize and return the IVsUIObject - it will save time and memory 
				// creating these objects.

				// Helper classses in Microsoft.Internal.VisualStudio.PlatformUI can be used to construct IVsUIObject of VsUIType.Icon
				// Use Win32IconUIObject if you have a HICON, use WinFormsIconUIObject if you have a System.Drawing.Icon, or
				// use WpfPropertyValue.CreateIconObject() if you have a WPF ImageSource.
				//return new WinFormsIconUIObject(this.item.Icon);
			}
		}

		public void InvokeAction()
		{
			// This function is called when the user selects the item result from the Quick Launch popup
			MessageBox.Show("Hello from " + DisplayText);
		}

		public string PersistenceData
		{
			get
			{
				return item.Id.ToString();
			}
		}

		public IVsSearchProvider SearchProvider
		{
			get { return provider; }
		}

		public string Tooltip
		{
			get { return null; }
		}

		#endregion IVsSearchItemResult

		public static IVsSearchItemResult CreateItemResult(string lpszPersistenceData, WorkItem[] items, IVsSearchProvider provider)
		{
			foreach (var item in items)
			{
				// Try to match the name, that we reported as persistence string
				if (item.Id.ToString().Equals(lpszPersistenceData, StringComparison.Ordinal))
				{
					// Create a new item. The item creation must be a fast operation (e.g. should not make network requests)
					return new WorkItemSearchResult(item, provider);
				}
			}

			// We got called with an item that we cannot recreate, return null
			return null;
		}

	}

}