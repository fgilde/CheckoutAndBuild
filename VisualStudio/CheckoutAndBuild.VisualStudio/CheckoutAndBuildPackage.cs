using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Settings;
using CheckoutAndBuild.VisualStudio.Common;
using CheckoutAndBuild.VisualStudio.ErrorList;
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
	[ProvideToolWindow(typeof(GitToolWindow))]
	[ProvideToolWindow(typeof(WorkItemToolWindow))]
	[ProvideOptionPage(typeof(CheckoutAndBuildOptionsPage), "CheckoutAndBuild", "General", 0, 0, true)]
	public sealed class CheckoutAndBuildPackage : AsyncPackage
	{
		public const string PackageGuidString = "13646d50-ef88-4777-9d09-e55b321cd24f";
		public static readonly Guid CommandSetGuid = new Guid("874acff0-be59-4dfc-8975-d77d0b75b5fe");
		public const int ShowMainWindowCommandId = 0x0100;
		public const int ClearErrorsCommandId = 0x0200;
		public const int ShowGitWindowCommandId = 0x0300;
		public const int ShowWorkItemWindowCommandId = 0x0400;

		private CoabErrorListProvider errorListProvider;

		/// <summary>Loaded package instance (set in InitializeAsync); used by the options page.</summary>
		internal static CheckoutAndBuildPackage Instance { get; private set; }

		/// <summary>Lazily created Error List provider (UI thread only).</summary>
		internal CoabErrorListProvider ErrorListProvider
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				if (errorListProvider == null && !DisposalToken.IsCancellationRequested)
					errorListProvider = new CoabErrorListProvider(this);
				return errorListProvider;
			}
		}

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			Instance = this;
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			CoabOutputPane.Initialize(this, JsonSettingsService.CreateDefault());

			if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
			{
				commandService.AddCommand(new MenuCommand(ShowMainWindow,
					new CommandID(CommandSetGuid, ShowMainWindowCommandId)));

				commandService.AddCommand(new MenuCommand(ShowGitWindow,
					new CommandID(CommandSetGuid, ShowGitWindowCommandId)));

				commandService.AddCommand(new MenuCommand(ShowWorkItemWindow,
					new CommandID(CommandSetGuid, ShowWorkItemWindowCommandId)));

				var clearErrors = new OleMenuCommand(ClearErrors, new CommandID(CommandSetGuid, ClearErrorsCommandId));
				clearErrors.BeforeQueryStatus += OnClearErrorsQueryStatus;
				commandService.AddCommand(clearErrors);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				errorListProvider?.Dispose();
				errorListProvider = null;
			}
			base.Dispose(disposing);
		}

		private void OnClearErrorsQueryStatus(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var command = (OleMenuCommand)sender;
			// use the field, not the property: never create the provider just to query status
			command.Visible = command.Enabled = errorListProvider != null && errorListProvider.HasTasks;
		}

		private void ClearErrors(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			errorListProvider?.Clear();
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

		private void ShowGitWindow(object sender, EventArgs e)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(GitToolWindow), 0, true, DisposalToken);
				if (window?.Frame == null)
					throw new NotSupportedException("Cannot create CheckoutAndBuild Git tool window.");
			}).FileAndForget("checkoutandbuild/showgitwindow");
		}

		private void ShowWorkItemWindow(object sender, EventArgs e)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(WorkItemToolWindow), 0, true, DisposalToken);
				if (window?.Frame == null)
					throw new NotSupportedException("Cannot create CheckoutAndBuild Work Items tool window.");
			}).FileAndForget("checkoutandbuild/showworkitemwindow");
		}

		/// <summary>Shows the git tool window on the History tab with the given repository selected.</summary>
		internal void ShowGitHistory(string repositoryPath)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(GitToolWindow), 0, true, DisposalToken);
				if (window?.Content is GitToolWindowControl control)
					await control.ShowHistoryAsync(repositoryPath);
			}).FileAndForget("checkoutandbuild/showgithistory");
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
