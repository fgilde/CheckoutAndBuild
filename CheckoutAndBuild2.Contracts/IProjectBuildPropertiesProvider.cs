using System.Collections.Generic;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.Contracts
{
	public interface IProjectBuildPropertiesProvider
	{
		IDictionary<string, string> GetDefaultBuildProperties(ISolutionProjectModel project, IServiceSettings settings);
	}
}