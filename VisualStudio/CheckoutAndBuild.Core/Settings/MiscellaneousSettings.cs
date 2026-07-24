using System.ComponentModel;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Settings
{
	public class MiscellaneousSettings : ISettingsProviderClass
	{
		[SettingsProperty(SettingsAvailability.ProjectSpecific, "PostServiceScript", "The Post-Service Script will be called after a service is Executed. You can use a Powershell script (*.ps1) as well. \n On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject, $result, $scriptPath)")]
		[DefaultValue("")]
		[Description("The Post-Service Script will be called after a service is Executed. You can use a Powershell script (*.ps1) as well. \n On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject, $result)")]
		[DisplayName(@"Post-Service Script")]
		[Category("Miscellaneous")]
		public string PostServiceScriptFile { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "UseBranchSpecificSettings", "When enabled, per-solution settings are stored per git branch (falling back to the branch-independent values). Old name: GitPerBranchSettings.")]
		[DefaultValue(false)]
		[Description("When enabled, per-solution settings are stored per git branch (falling back to the branch-independent values).")]
		[DisplayName(@"Use branch specific settings")]
		[Category("Miscellaneous")]
		public bool UseBranchSpecificSettings { get; set; }

		[SettingsProperty(SettingsAvailability.ProjectSpecific, "PreServiceScript", "The Pre-Service Script will be called before a service starts execution for a project. You can use a Powershell script (*.ps1) as well. On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject, $scriptPath)")]
		[DefaultValue("")]
		[Description("The Pre-Service Script will be called before a service starts execution for a project. You can use a Powershell script (*.ps1) as well. On a powershell script At the top of your file, add this line: param($service, $solutionPath, $solutionObject)")]
		[DisplayName(@"Pre-Service Script")]
		[Category("Miscellaneous")]
		public string PreServiceScriptFile { get; set; }
	}
}
