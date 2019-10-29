namespace CheckoutAndBuild2.Contracts.Service
{
	public interface IDefaultBuildPriorityManager
	{		
		int GetDefaultBuildPriority(ISolutionProjectModel solutionFile);
	}
}