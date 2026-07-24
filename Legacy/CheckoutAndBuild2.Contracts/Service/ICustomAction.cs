using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.Contracts.Service
{
	public interface ICustomAction
	{	
        void RunPostAction(IOperationService service, ISolutionProjectModel solutionFile, object result, IServiceSettings settings);

        void RunPreAction(IOperationService service, ISolutionProjectModel solutionFile, IServiceSettings settings);
    }
}