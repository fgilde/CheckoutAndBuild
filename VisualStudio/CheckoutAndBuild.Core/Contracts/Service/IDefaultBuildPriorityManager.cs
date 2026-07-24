namespace CheckoutAndBuild.Core.Contracts.Service
{
	public interface IDefaultBuildPriorityManager
	{
		int GetDefaultBuildPriority(ISolutionProjectModel solutionFile);
	}
}
