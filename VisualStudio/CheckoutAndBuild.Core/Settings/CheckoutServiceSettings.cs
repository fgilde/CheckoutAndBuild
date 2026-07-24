using System.ComponentModel;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Settings
{
	public class CheckoutServiceSettings : ISettingsProviderClass
	{
		[SettingsProperty(SettingsAvailability.Global, "Force and Overwrite", "Check this to true to checkout everything with an overwrite flag", ServiceId = ServiceIds.CheckoutServiceId)]
		[DefaultValue(false)]
		public bool ForceAndOverwrite { get; set; }
	}
}
