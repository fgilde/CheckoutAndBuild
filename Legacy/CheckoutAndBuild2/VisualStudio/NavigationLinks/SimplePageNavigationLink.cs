using System;
using System.ComponentModel.Composition;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.VisualStudio.NavigationLinks
{
	public abstract class SimplePageNavigationLink : BaseViewModel, ITeamExplorerNavigationLink
	{

		private string text;
		private object context;

		public virtual Guid PageIdToNavigate { get { return Guid.Empty; } }

		public virtual string Text
		{
			get { return text; }
			set { SetProperty(ref text, value); }
		}

		public virtual object Context
		{
			get { return context; }
			set { SetProperty(ref context, value); }
		}

	    /// <summary>
        /// Constructor.
        /// </summary>
        [ImportingConstructor]
	    protected SimplePageNavigationLink([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
			:base(serviceProvider)
	    {}

        public virtual void Execute()
        {
            // Navigate to the recent changes page
			ITeamExplorer teamExplorer = serviceProvider.Get<ITeamExplorer>();
            if (teamExplorer != null && PageIdToNavigate != Guid.Empty)
				teamExplorer.NavigateToPage(PageIdToNavigate, Context);				
        }


		public virtual bool IsEnabled { get { return true; } }

		public virtual bool IsVisible { get { return true; } }

		public virtual void Invalidate()
        {}
		public virtual void Dispose()
	    {}
	}
}