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

		[SettingsProperty(SettingsAvailability.Global, "ScheduledRunEnabled", "Run the pipeline automatically once per day at the scheduled time (morning build).")]
		[DefaultValue(false)]
		[Description("Run the pipeline automatically once per day at the scheduled time.")]
		[DisplayName(@"Scheduled run enabled")]
		[Category("Miscellaneous")]
		public bool ScheduledRunEnabled { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "ScheduledRunTime", "Time of day (HH:mm, 24h) for the scheduled pipeline run.")]
		[DefaultValue("08:00")]
		[Description("Time of day (HH:mm, 24h) for the scheduled pipeline run.")]
		[DisplayName(@"Scheduled run time")]
		[Category("Miscellaneous")]
		public string ScheduledRunTime { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "SkipUnchanged", "After the checkout/pull step, skip restore, build and test for solutions whose repository received no new commits. A clean step still runs for all solutions.")]
		[DefaultValue(false)]
		[Description("After the checkout/pull step, skip restore, build and test for solutions whose repository received no new commits.")]
		[DisplayName(@"Skip unchanged repositories")]
		[Category("Miscellaneous")]
		public bool SkipUnchanged { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "AutoStash", "Stash uncommitted changes automatically before pull and branch checkout and restore them afterwards. A conflicting restore keeps the changes safely in stash@{0}.")]
		[DefaultValue(true)]
		[Description("Stash uncommitted changes automatically around pull and branch checkout and restore them afterwards.")]
		[DisplayName(@"Auto-stash around pull/checkout")]
		[Category("Miscellaneous")]
		public bool AutoStash { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "WatchModeEnabled", "Watch mode: fetch all repositories periodically and run the pipeline automatically when a repository is behind its upstream. Combine with 'Skip unchanged repositories' to rebuild only what changed.")]
		[DefaultValue(false)]
		[Description("Fetch all repositories periodically and run the pipeline automatically when a repository is behind its upstream.")]
		[DisplayName(@"Watch mode enabled")]
		[Category("Miscellaneous")]
		public bool WatchModeEnabled { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "WatchIntervalMinutes", "Interval in minutes between watch-mode fetches.")]
		[DefaultValue(10)]
		[Description("Interval in minutes between watch-mode fetches.")]
		[DisplayName(@"Watch interval (minutes)")]
		[Category("Miscellaneous")]
		public int WatchIntervalMinutes { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "LogLevel", "Verbosity for the CheckoutAndBuild output window and msbuild (Quiet/Minimal/Normal/Detailed/Diagnostic).")]
		[DefaultValue(LoggerVerbosity.Minimal)]
		[Description("Verbosity for the CheckoutAndBuild output window and msbuild.")]
		[DisplayName(@"Log level")]
		[Category("Miscellaneous")]
		public LoggerVerbosity LogLevel { get; set; }

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
