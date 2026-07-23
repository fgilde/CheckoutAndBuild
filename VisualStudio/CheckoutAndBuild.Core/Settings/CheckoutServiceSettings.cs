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

		[SettingsProperty(SettingsAvailability.Global, "Prompt for merge", "Set this to true to get the Possibility to merge your Conflicts after checkout", ServiceId = ServiceIds.CheckoutServiceId)]
		[DefaultValue(true)]
		public bool PromptForMerge { get; set; }
	}
}
