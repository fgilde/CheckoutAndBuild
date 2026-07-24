using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.CP.Plugin
{
	public class CPSettings : ISettingsProviderClass
	{

		[SettingsProperty(SettingsAvailability.Global, "Register Com", ServiceId = ServiceIds.BuildServiceId), DefaultValue(false)]		
		public bool RegisterCom { get; set; }

        [SettingsProperty(SettingsAvailability.Global, "DeleteTypeLibs", ServiceId = ServiceIds.BuildServiceId), DefaultValue(false)]
        public bool DeleteTypeLibs { get; set; }

        [SettingsProperty(SettingsAvailability.Global, "Automatic Backup/Restore", "Creates automatically a Backup for ProgramData, UserData, XML etc and restores it after build", ServiceId = ServiceIds.CleanServiceId), DefaultValue(true)]
		public bool AutoBackup { get; set; }

	    [SettingsProperty(SettingsAvailability.Global, "SetupDelphiMagic", "If this is true CP Magic for Delphi crazy stuff is enabled", ServiceId = ServiceIds.BuildServiceId), DefaultValue(true)]
	    public bool SetupDelphiMagic { get; set; }

        //[SettingsProperty(SettingsAvailability.Global, "Buildorder Templatefile", "Templatefile for Default Buildorder", ServiceId = ServiceIds.BuildServiceId), DefaultValue(@"\\cpws01b\cpintern\FlorianG_Fach\!coab\templates\coab_buildorder.txt")]
        //[Description("Templatefile for Default Buildorder")]
        //[DisplayName(@"Buildorder Templatefile")]	    
        //[Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string BuildOrderTemplate { get; set; }
    }
}