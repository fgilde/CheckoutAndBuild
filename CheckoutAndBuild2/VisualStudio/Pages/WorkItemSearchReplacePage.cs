using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
	[TeamExplorerPage(GuidList.workItemSearchReplacePageId)]
    public class WorkItemSearchReplacePage : TeamExplorerBase
    {
        private WorkItemSearchReplaceViewModel[] viewModels;
		private ItemsControl ctrl;
		private readonly Dictionary<PropertyChangedEventHandler,INotifyPropertyChanged> eventHandlers = new Dictionary<PropertyChangedEventHandler, INotifyPropertyChanged>();

		public override string Title => "Search and Replace in WorkItems";

        public override void Initialize(object sender, IServiceProvider provider, object context)
        {
            base.Initialize(sender, provider, context);

            if (context is IEnumerable<QueryItem> queryItems)
			{				
				viewModels = queryItems.Select(item =>
				{
					var viewModel = new WorkItemSearchReplaceViewModel(item, provider);
					eventHandlers.Add(viewModel.OnChange(() => viewModel.IsBusy, b => IsBusy = b), viewModel);
					return viewModel;
				}).ToArray();
			}
			ctrl = new ItemsControl {ItemsSource = viewModels};
        }

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public override void Dispose()
		{
			DetachHandlers();
			base.Dispose();
		}

		public override object Content => ctrl;

        private void DetachHandlers()
		{
			foreach (KeyValuePair<PropertyChangedEventHandler, INotifyPropertyChanged> handler in eventHandlers)
				handler.Value.PropertyChanged -= handler.Key;
			eventHandlers.Clear();
		}
    }
}
