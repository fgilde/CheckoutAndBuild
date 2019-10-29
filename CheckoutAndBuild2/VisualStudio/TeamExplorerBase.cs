using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	public abstract class TeamExplorerBase : NotificationObject, ITeamExplorerPage, ITeamExplorerSection
	{
	    private const int secondsToWaitBeforeExtendUI = 2;
		private bool isBusy;
		private bool isExpanded = true;
		private bool isVisible = true;
		private bool contextIsSubscribed = false;
		private bool isEnabled = true;
		private string errorMessage;
		private PropertyChangedEventHandler pageChangedEventHandler;

		internal object CurrentIntitializeSender { get; private set; }

		protected IServiceProvider serviceProvider { get; private set; }

		internal ITeamExplorer TeamExplorer => serviceProvider.Get<ITeamExplorer>();

	    internal TfsContext TfsContext => serviceProvider.Get<TfsContext>();


	    public abstract string Title { get; }

		public virtual object Content => null;

	    public object PageContent => Content;
	    public object SectionContent => Content;

	    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	    protected async Task WaitForLoad()
	    {
	        await Task.Delay(TimeSpan.FromSeconds(secondsToWaitBeforeExtendUI));
        }

	    public bool IsCurrentPageInSectionPlacement()
	    {
	        var ids = GetType().GetAttributes<TeamExplorerSectionAttribute>(false).Select(a => a.ParentPageId)
                .Concat(GetType().GetAttributes<TeamExplorerSectionPlacementAttribute>(false).Select(a => a.PlacementParentPageId))
                .Where(s => !string.IsNullOrEmpty(s) && Guid.TryParse(s, out Guid g)).Select(Guid.Parse);
	        return ids.Any(IsCurrentPageInSectionPlacement);
	    }


        public bool IsCurrentPageInSectionPlacement(Guid pageId)
	    {
	        return TeamExplorer.CurrentPage != null && TeamExplorer.CurrentPage.GetId() == pageId;
	    }

	    public string ErrorMessage
		{
			get { return errorMessage; }
			set
			{
				if (SetProperty(ref errorMessage, value))
					RaisePropertiesChanged(() => HasError);
			}
		}


		public virtual void Initialize(object sender, IServiceProvider provider, object context)
		{
			CurrentIntitializeSender = sender;
			if (serviceProvider != null)
				UnsubscribeContextChanges();

			serviceProvider = provider;

			if (provider != null)
				SubscribeContextChanges();
			if (TeamExplorer != null)
			{
				pageChangedEventHandler = TeamExplorer?.OnChange(() => TeamExplorer.CurrentPage, OnPageChanged, true);
			}
		}

		protected virtual void OnPageChanged(ITeamExplorerPage obj)
		{}

		public void Initialize(object sender, PageInitializeEventArgs e)
		{
			Initialize(sender, e.ServiceProvider, e.Context);
		}

		public void Initialize(object sender, SectionInitializeEventArgs e)
		{
			Initialize(sender, e.ServiceProvider, e.Context);
		}

		public bool IsEnabled
		{
			get { return isEnabled; }
			set
			{
				SetProperty(ref isEnabled, value);
				if (!IsEnabled && IsBusy)
					IsBusy = false;
			}
		}

		public bool IsBusy
		{
			get
			{
				if (!IsEnabled && isBusy)
					isBusy = false;
				return isBusy;
			}
			set
			{
				if (IsEnabled || !value)
					SetProperty(ref isBusy, value);
			}
		}

		public bool IsExpanded
		{
			get { return isExpanded; }
			set { SetProperty(ref isExpanded, value); }
		}

		public bool IsVisible
		{
			get { return isVisible; }
			set { SetProperty(ref isVisible, value); }
		}

		public virtual object GetExtensibilityService(Type serviceType)
		{
			return null;
		}

		public virtual void Loaded(object sender, SectionLoadedEventArgs e)
		{ }

		public virtual void SaveContext(object sender, SectionSaveContextEventArgs e)
		{ }

		public virtual void Cancel()
		{ }

		public virtual void Loaded(object sender, PageLoadedEventArgs e)
		{ }

		public virtual void Refresh()
		{ }

		public virtual void SaveContext(object sender, PageSaveContextEventArgs e)
		{ }

		public virtual void Dispose()
		{
			if (TeamExplorer != null && pageChangedEventHandler != null)
				TeamExplorer.PropertyChanged -= pageChangedEventHandler;
		}

		/// <summary>
		/// Subscribe to context changes.
		/// </summary>
		protected void SubscribeContextChanges()
		{
			if (serviceProvider == null || contextIsSubscribed)
			{
				return;
			}

			ITeamFoundationContextManager tfContextManager = GetService<ITeamFoundationContextManager>();
			if (tfContextManager != null)
			{
				tfContextManager.ContextChanged += ContextChanged;
				contextIsSubscribed = true;
			}
		}

		/// <summary>
		/// Unsubscribe from context changes.
		/// </summary>
		protected void UnsubscribeContextChanges()
		{
			if (serviceProvider == null || !contextIsSubscribed)
			{
				return;
			}

			ITeamFoundationContextManager tfContextManager = GetService<ITeamFoundationContextManager>();
			if (tfContextManager != null)
			{
				tfContextManager.ContextChanged -= ContextChanged;
			}
		}


		/// <summary>
		/// ContextChanged event handler.
		/// </summary>
		protected virtual void ContextChanged(object sender, ContextChangedEventArgs e)
		{ }

		public T GetService<T>()
		{
			if (serviceProvider != null)
			{
				return serviceProvider.Get<T>();
			}

			return default(T);
		}

		/// <summary>
		/// Get the current Team Foundation context.
		/// </summary>
		protected ITeamFoundationContext CurrentContext
		{
			get
			{
				ITeamFoundationContextManager tfContextManager = GetService<ITeamFoundationContextManager>();
			    return tfContextManager?.CurrentContext;
			}
		}

	}
}