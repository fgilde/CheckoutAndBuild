using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Contracts.Service
{
	public interface ICustomAction
	{
		void RunPostAction(IOperationService service, ISolutionProjectModel solutionFile, object result, IServiceSettings settings);

		void RunPreAction(IOperationService service, ISolutionProjectModel solutionFile, IServiceSettings settings);
	}
}
