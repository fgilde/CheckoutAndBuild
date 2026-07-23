using System.Collections.Generic;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Contracts
{
	public interface IProjectBuildPropertiesProvider
	{
		IDictionary<string, string> GetDefaultBuildProperties(ISolutionProjectModel project, IServiceSettings settings);
	}
}
