using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace FG.CheckoutAndBuild2.Types
{
    public class MiscellaneousSettings : ISettingsProviderClass
    {

        [SettingsProperty(SettingsAvailability.ProjectSpecific, "PostServiceScript", "The Post-Service Script will be called after a service is Executed. You can use a Powershell script (*.ps1) as well. \n On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject, $result, $scriptPath)")]
        [DefaultValue("")]
        [Description("The Post-Service Script will be called after a service is Executed. You can use a Powershell script (*.ps1) as well. \n On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject, $result)")]
        [DisplayName(@"Post-Service Script")]
        [Category("Miscellaneous")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string PostServiceScriptFile { get; set; }

        [SettingsProperty(SettingsAvailability.ProjectSpecific, "PreServiceScript", "The Pre-Service Script will be called before a service starts execution for a project. You can use a Powershell script (*.ps1) as well. On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject, $scriptPath)")]
        [DefaultValue("")]
        [Description("The Pre-Service Script will be called before a service starts execution for a project. You can use a Powershell script (*.ps1) as well. On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject)")]
        [DisplayName(@"Pre-Service Script")]        
        [Category("Miscellaneous")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string PreServiceScriptFile { get; set; }
    }
}