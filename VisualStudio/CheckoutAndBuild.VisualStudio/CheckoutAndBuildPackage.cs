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
		public const int ExportSolutionPatchCommandId = 0x0500;
		public const int ExportSolutionZipCommandId = 0x0510;
		public const int OpenSolutionInGitCommandId = 0x0520;

		private CoabErrorListProvider errorListProvider;

		internal static CheckoutAndBuildPackage Instance { get; private set; }

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

				commandService.AddCommand(new MenuCommand(
					(s, e) => ExportSolutionChanges(asZip: false),
					new CommandID(CommandSetGuid, ExportSolutionPatchCommandId)));
				commandService.AddCommand(new MenuCommand(
					(s, e) => ExportSolutionChanges(asZip: true),
					new CommandID(CommandSetGuid, ExportSolutionZipCommandId)));
				commandService.AddCommand(new MenuCommand(
					(s, e) => OpenSolutionInGit(),
					new CommandID(CommandSetGuid, OpenSolutionInGitCommandId)));
			}
		}

		/// <summary>Repository root of the solution currently open in this instance, or null (with a message shown).</summary>
		private string GetActiveSolutionRepository()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string directory = null;
			if (GetGlobalService(typeof(Microsoft.VisualStudio.Shell.Interop.SVsSolution)) is Microsoft.VisualStudio.Shell.Interop.IVsSolution solution)
				solution.GetSolutionInfo(out directory, out _, out _);
			string root = null;
			if (!string.IsNullOrEmpty(directory))
			{
				try { root = new CheckoutAndBuild.Core.Git.GitService().GetRepositoryRoot(directory); }
				catch (Exception) { }
			}
			if (string.IsNullOrEmpty(root))
				VsShellUtilities.ShowMessageBox(this,
					"The open solution is not inside a git repository (or no solution is open).",
					"CheckoutAndBuild", Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_INFO,
					Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK,
					Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
			return string.IsNullOrEmpty(root) ? null : root;
		}

		/// <summary>Exports the uncommitted changes of the open solution's repository as .patch or .zip.</summary>
		private void ExportSolutionChanges(bool asZip)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string root = GetActiveSolutionRepository();
			if (root == null)
				return;
			string name = System.IO.Path.GetFileName(root.TrimEnd(System.IO.Path.DirectorySeparatorChar));
			var dialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = asZip ? "Zip archive|*.zip" : "Patch file|*.patch",
				FileName = name + "-changes" + (asZip ? ".zip" : ".patch"),
				DefaultExt = asZip ? ".zip" : ".patch"
			};
			if (dialog.ShowDialog() != true)
				return;
			JoinableTaskFactory.RunAsync(async () =>
			{
				var git = new CheckoutAndBuild.Core.Git.GitService();
				if (asZip)
					await git.ExportChangesAsZipAsync(root, dialog.FileName);
				else
					await git.ExportChangesAsPatchAsync(root, dialog.FileName);
				await JoinableTaskFactory.SwitchToMainThreadAsync();
				VsShellUtilities.ShowMessageBox(this,
					"Exported: " + dialog.FileName,
					"CheckoutAndBuild", Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_INFO,
					Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK,
					Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
			}).FileAndForget("checkoutandbuild/exportsolutionchanges");
		}

		private void OpenSolutionInGit()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string root = GetActiveSolutionRepository();
			if (root != null)
				ShowGitRepository(root);
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

		internal void ShowGitHistory(string repositoryPath)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(GitToolWindow), 0, true, DisposalToken);
				if (window?.Content is GitToolWindowControl control)
					await control.ShowHistoryAsync(repositoryPath);
			}).FileAndForget("checkoutandbuild/showgithistory");
		}

		internal void ShowGitRepository(string repositoryPath)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(GitToolWindow), 0, true, DisposalToken);
				if (window?.Content is GitToolWindowControl control)
					await control.ShowRepositoryAsync(repositoryPath);
			}).FileAndForget("checkoutandbuild/showgitrepository");
		}

		internal void ShowGitWorktrees(string repositoryPath)
		{
			JoinableTaskFactory.RunAsync(async () =>
			{
				var window = await ShowToolWindowAsync(typeof(GitToolWindow), 0, true, DisposalToken);
				if (window?.Content is GitToolWindowControl control)
					await control.ShowWorktreesAsync(repositoryPath);
			}).FileAndForget("checkoutandbuild/showgitworktrees");
		}

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
