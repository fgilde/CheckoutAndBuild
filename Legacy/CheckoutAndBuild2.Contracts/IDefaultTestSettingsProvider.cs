using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.Contracts
{
	public interface IDefaultTestSettingsProvider
	{
		string GetTestSettingsFile(ISolutionProjectModel project, IServiceSettings settings);
	}
}