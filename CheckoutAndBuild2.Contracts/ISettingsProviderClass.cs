using System.ComponentModel.Composition;

namespace CheckoutAndBuild2.Contracts
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