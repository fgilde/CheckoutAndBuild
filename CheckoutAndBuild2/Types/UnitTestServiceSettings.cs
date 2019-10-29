using System.ComponentModel;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace FG.CheckoutAndBuild2.Types
{
	public class UnitTestServiceSettings : ISettingsProviderClass
	{
		[SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "Require Admin Privileges", "If this option is set to true the MSTest Processes will be executed with administrative  privileges", ServiceId = ServiceIds.TestServiceId)]
		[DefaultValue(false)]
		[DisplayName("Require Admin Privileges")]
		[Category("UnitTest")]
		public bool RequiresAdminPrivileges { get; set; }

		[SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "Cancel TestRun if one test fails", "Set this to true if you want to cancel test run if one test has failed ", ServiceId = ServiceIds.TestServiceId)]
		[DefaultValue(false)]
		[DisplayName("Cancel TestRun if one test fails")]
		[Category("UnitTest")]
		public bool CancelOnFailures { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "Track live output", "Set this to true to track live output in console, otherwise output will be written after complete test run. (Performance is slower with live output) ", ServiceId = ServiceIds.TestServiceId)]
		[DefaultValue(true)]
		[DisplayName("Track live output")]
		[Category("UnitTest")]
		public bool TrackLiveOutput { get; set; }
	}
}