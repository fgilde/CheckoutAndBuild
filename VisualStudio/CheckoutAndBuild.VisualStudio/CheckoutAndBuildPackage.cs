using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.VisualStudio.Options;
using CheckoutAndBuild.VisualStudio.ToolWindows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CheckoutAndBuild.VisualStudio
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid(PackageGuidString)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideToolWindow(typeof(MainToolWindow))]
	[ProvideOptionPage(typeof(CheckoutAndBuildOptionsPage), "CheckoutAndBuild", "General", 0, 0, true)]
	public sealed class CheckoutAndBuildPackage : AsyncPackage
	{
		public const string PackageGuidString = "13646d50-ef88-4777-9d09-e55b321cd24f";
		public static readonly Guid CommandSetGuid = new Guid("874acff0-be59-4dfc-8975-d77d0b75b5fe");
		public const int ShowMainWindowCommandId = 0x0100;

		/// <summary>Loaded package instance (set in InitializeAsync); used by the options page.</summary>
		internal static CheckoutAndBuildPackage Instance { get; private set; }

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			Instance = this;
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
			{
				commandService.AddCommand(new MenuCommand(ShowMainWindow,
					new CommandID(CommandSetGuid, ShowMainWindowCommandId)));
			}
		}

		private void ShowMainWindow(object sender, EventArgs e)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(MainToolWindow), 0, true, DisposalToken);
				if (window?.Frame == null)
					throw new NotSupportedException("Cannot create CheckoutAndBuild tool window.");
			}).FileAndForget("checkoutandbuild/showmainwindow");
		}

		/// <summary>Shows the tool window with the settings view opened (Tools → Options link).</summary>
		internal void ShowMainWindowSettings()
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(MainToolWindow), 0, true, DisposalToken);
				(window?.Content as MainToolWindowControl)?.ShowSettings();
			}).FileAndForget("checkoutandbuild/showsettings");
		}
	}
}
