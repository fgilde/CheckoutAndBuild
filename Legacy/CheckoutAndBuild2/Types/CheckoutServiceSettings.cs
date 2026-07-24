using System.ComponentModel;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace FG.CheckoutAndBuild2.Types
{
	public class CheckoutServiceSettings : ISettingsProviderClass
	{
		[SettingsProperty(SettingsAvailability.Global, "Force and Overwrite", "Check this to true to checkout everything with an overwrite flag", ServiceId = ServiceIds.CheckoutServiceId)]
		[DefaultValue(false)]
		public bool ForceAndOverwrite { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "Checkout whole Workingfolder", "Set this to true to checkout the complete Workingfolder, othwerwise included solutions folder only will be checked out", ServiceId = ServiceIds.CheckoutServiceId)]
		[DefaultValue(false)]
		public bool CheckoutWorkingfolder { get; set; }


		[SettingsProperty(SettingsAvailability.Global, "Prompt for merge", "Set this to true to get the Possibility to merge your Conflicts after checkout", ServiceId = ServiceIds.CheckoutServiceId)]
		[DefaultValue(true)]
		public bool PromptForMerge { get; set; }
	}
}