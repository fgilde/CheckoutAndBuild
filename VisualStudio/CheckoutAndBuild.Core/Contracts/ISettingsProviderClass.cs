using System.ComponentModel.Composition;

namespace CheckoutAndBuild.Core.Contracts
{
	[InheritedExport]
	public interface ISettingsProviderClass
	{}

	public enum SettingsAvailability
	{
		Global,
		ProjectSpecific,
		GlobalWithProjectSpecificOverride
	}

}
