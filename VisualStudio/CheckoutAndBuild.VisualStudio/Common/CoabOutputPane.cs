using System;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Settings;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CheckoutAndBuild.VisualStudio.Common
{
	/// <summary>
	/// "CheckoutAndBuild" pane in the VS Output window fed by <see cref="CoabLog"/>,
	/// filtered by the LogLevel setting (port of the old Output.cs pane).
	/// </summary>
	internal static class CoabOutputPane
	{
		private static readonly Guid paneGuid = new Guid("d3c7bcb4-11a4-4b46-9b19-1c1c7f6dbd67");
		private static IVsOutputWindowPane pane;
		private static ISettingsService settingsService;
		private static readonly SettingsContext globalContext = new SettingsContext();

		/// <summary>Creates the pane and subscribes to CoabLog. Must run on the UI thread (package init).</summary>
		public static void Initialize(IServiceProvider provider, ISettingsService settings)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (pane != null)
				return;
			settingsService = settings;

			if (provider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outputWindow)
			{
				Guid guid = paneGuid;
				ErrorHandler.ThrowOnFailure(outputWindow.CreatePane(ref guid, "CheckoutAndBuild", fInitVisible: 1, fClearWithSolution: 0));
				outputWindow.GetPane(ref guid, out pane);
			}
			if (pane != null)
				CoabLog.MessageLogged += OnMessage;
		}

		private static void OnMessage(LoggerVerbosity level, string message)
		{
			var configured = settingsService?.Get("LogLevel", globalContext, LoggerVerbosity.Minimal) ?? LoggerVerbosity.Minimal;
			if (level > configured)
				return;
#pragma warning disable VSTHRD010 // OutputStringThreadSafe is callable from any thread
			pane.OutputStringThreadSafe(message + Environment.NewLine);
#pragma warning restore VSTHRD010
		}
	}
}
