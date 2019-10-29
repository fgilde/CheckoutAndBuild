using System.ComponentModel;
using System.Drawing.Design;
using System.Threading;
using System.Windows.Forms.Design;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace FG.CheckoutAndBuild2.Types
{
	public class BuildServiceSettings : ISettingsProviderClass
	{
		[SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "Cancel Queued on Failures", "If this option is set to true all queued builds will be cancelled if one build fails ", ServiceId = ServiceIds.BuildServiceId)]
		[DefaultValue(false)]
		[DisplayName("Cancel Queued on Failures")]
		[Category("Build Settings")]
		public bool CancelQueuedOnFailures { get; set; }

		[SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "Kill dependent processes", "This feature will kill the processes that have locked the output file or the outputfile itself", ServiceId = ServiceIds.BuildServiceId)]
		[DefaultValue(true)]
		[DisplayName("Kill dependent processes")]
		[Category("Build Settings")]
		public bool KillDependendProcesses { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "Enable Node Reuse", "A flag determining whether out-of-process nodes should persist after the build and wait for further builds.", ServiceId = ServiceIds.BuildServiceId)]
		[DefaultValue(false)]
		[DisplayName("Enable Node Reuse")]
		[Category("Build Settings")]
		public bool EnableNodeReuse { get; set; }

		[SettingsProperty(SettingsAvailability.Global, "Max Node Count", "The maximum number of nodes this build may use. (use 0 to use ProcessorCount) ", ServiceId = ServiceIds.BuildServiceId)]
		[DefaultValue(0)]
		[DisplayName("Max Node Count")]
		[Category("Build Settings")]
		public int MaxNodeCount { get; set; }


		[SettingsProperty(SettingsAvailability.Global, "Thread Priority", "The desired thread priority for building.", ServiceId = ServiceIds.BuildServiceId)]
		[DefaultValue(ThreadPriority.Highest)]
		[DisplayName("Thread Priority")]
		[Category("Build Settings")]
		public ThreadPriority ThreadPriority { get; set; }

        [SettingsProperty(SettingsAvailability.GlobalWithProjectSpecificOverride, "BuildMode", "BuildMode to use (Default = Async build by Priority, MergedBuild = Merge Solution to One Build Solution and Builds this with auto dependency detection)", ServiceId = ServiceIds.BuildServiceId)]
        [DefaultValue(BuildMode.Default)]
        [DisplayName("BuildMode")]
        [Category("Build Settings")]
        public BuildMode BuildMode { get; set; }
    }

    public enum BuildMode
    {
        Default = 0,
        MergedBuild = 1
    }
}