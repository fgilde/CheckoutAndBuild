using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.ViewModels;

namespace FG.CheckoutAndBuild2.Services
{
	public abstract class BaseService
	{

	    private ConcurrentBag<ISolutionProjectModel> cancelledSolutions;
		protected readonly IServiceProvider serviceProvider;
		protected readonly object lockObj = new object();
		protected readonly SettingsService settingsService;
		protected readonly TfsContext tfsContext;
		protected readonly MainLogic mainLogic;
		protected readonly GlobalStatusService statusService;

	    public PausableCancellationTokenSource ServiceCancellationTokenSource { get; protected set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		protected BaseService(IServiceProvider serviceProvider)
		{
			this.serviceProvider = serviceProvider;
			settingsService = this.serviceProvider.Get<SettingsService>();
			mainLogic = this.serviceProvider.Get<MainLogic>();
			statusService = serviceProvider.Get<GlobalStatusService>();
			tfsContext = this.serviceProvider.Get<TfsContext>();
		}

		protected BaseService(): this(CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>())
		{}

        public async Task ExecuteAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, 
            CancellationToken globalCancellationToken)
        {
            var solutionProjectModels = solutionProjects as ISolutionProjectModel[] ?? solutionProjects.ToArray();            
            cancelledSolutions = new ConcurrentBag<ISolutionProjectModel>();
            ServiceCancellationTokenSource = PausableCancellationTokenSource.CreateLinkedTokenSource(globalCancellationToken);
            ServiceCancellationTokenSource?.Token.Register(() => solutionProjectModels.SetOperations(Operations.Cancelling));
            ServiceCancellationTokenSource?.Token.RegisterPaused((t, paused) => solutionProjectModels.OfType<ProjectViewModel>().Apply(model => model.SetPaused(paused)));
                        
            
            await globalCancellationToken.WaitWhenPaused();

            if (!IsCancelled(solutionProjectModels, globalCancellationToken))
                await ExecuteCoreAsync(solutionProjectModels, settings, ServiceCancellationTokenSource.Token);
        }

	    protected abstract Task ExecuteCoreAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, CancellationToken cancellationToken);
        

	    protected bool IsCancelled(ISolutionProjectModel projectViewModel, CancellationToken cancellationToken)
		{
			cancellationToken.Register(() =>
			{
                if(!IsCancelled(projectViewModel))
			        projectViewModel.CurrentOperation = Operations.Cancelling;
			});	        
	        if (cancellationToken.IsCancellationRequested || (ServiceCancellationTokenSource != null && ServiceCancellationTokenSource.Token.IsCancellationRequested))
				return true;
			return IsCancelled(projectViewModel);
		}

	    public bool IsCancelled(ISolutionProjectModel projectViewModel)
	    {
	        return cancelledSolutions != null && cancelledSolutions.Contains(projectViewModel);
	    }

	    public void Cancel(ISolutionProjectModel projectViewModel)
	    {
            if(cancelledSolutions == null)
                cancelledSolutions = new ConcurrentBag<ISolutionProjectModel>();
            cancelledSolutions.Add(projectViewModel);
	    }


        protected bool IsCancelled(IEnumerable<ISolutionProjectModel> projectViewModels, CancellationToken cancellationToken)
		{
			cancellationToken.Register(() => projectViewModels.Where(model => !IsCancelled(model)).SetOperations(Operations.Cancelling));
            
			if (cancellationToken.IsCancellationRequested || (ServiceCancellationTokenSource != null && ServiceCancellationTokenSource.Token.IsCancellationRequested))
				return true;
			return false;
		}

	    public void Cancel()
	    {
            ServiceCancellationTokenSource.Cancel();
        }

	}
}