using System.ComponentModel;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Settings
{
	public class CleanServiceSettings : ISettingsProviderClass
	{
		[SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "Custom Pathes to clean", "Enter here your Custom Pathes you want to clean. It is possible to add the path as Relative path, this path will then combined with the current solution directory", ServiceId = ServiceIds.CleanServiceId)]
		[DefaultValue(new string[0])]
		[Browsable(true)]
		[DisplayName("CustomCleanPathes")]
		[Category("Clean Settings")]
		public string[] CustomCleanPathes { get; set; }
	}
}
