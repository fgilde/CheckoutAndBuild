using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Contracts
{
	public interface IDefaultTestSettingsProvider
	{
		string GetTestSettingsFile(ISolutionProjectModel project, IServiceSettings settings);
	}
}
