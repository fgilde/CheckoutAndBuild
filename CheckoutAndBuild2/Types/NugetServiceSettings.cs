using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace FG.CheckoutAndBuild2.Types
{
    public class NugetServiceSettings : ISettingsProviderClass
    {
        [SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "Nuget.exe", "Path to Nuget.exe to use", ServiceId = ServiceIds.NugetRestoreServiceId)]
        [DefaultValue("")]
        [Description("Path to Nuget.exe to use")]
        [DisplayName(@"Nuget.exe")]
        [Category("Nuget")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string NugetExeLocation { get; set; }


        [SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "NugetAction", "Command that nuget should use", ServiceId = ServiceIds.NugetRestoreServiceId)]
        [DefaultValue(NugetAction.InstallAndRestore)]
        [DisplayName("NugetAction")]
        [Category("Nuget")]
        public NugetAction NugetAction { get; set; }

    }

    public enum NugetAction
    {
        Restore,
        Install,
        InstallAndRestore,
        Reinstall
    }
}