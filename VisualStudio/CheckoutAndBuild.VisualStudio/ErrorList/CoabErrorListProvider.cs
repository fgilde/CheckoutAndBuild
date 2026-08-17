using System;
using System.Collections.Generic;
using System.IO;
using CheckoutAndBuild.Core.Services;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CheckoutAndBuild.VisualStudio.ErrorList
{
	/// <summary>
	/// Reports build errors and test failures to the Visual Studio Error List
	/// (thin wrapper around <see cref="ErrorListProvider"/>; no reflection).
	/// All members must be called on the UI thread.
	/// </summary>
	internal sealed class CoabErrorListProvider : IDisposable
	{
		private readonly IServiceProvider serviceProvider;
		private readonly ErrorListProvider provider;

		public CoabErrorListProvider(IServiceProvider serviceProvider)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
			provider = new ErrorListProvider(serviceProvider)
			{
				ProviderName = "CheckoutAndBuild",
				ProviderGuid = new Guid("b5c9f3a1-6f2e-4d0c-9b7d-3a8f4e21c6d5")
			};
		}

		public bool HasTasks
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return provider.Tasks.Count > 0;
			}
		}

		/// <summary>Adds one Error List entry per build error/warning with double-click navigation.</summary>
		public void Report(IEnumerable<BuildError> errors)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			provider.SuspendRefresh();
			try
			{
				foreach (var error in errors)
				{
					var task = new ErrorTask
					{
						Category = TaskCategory.BuildCompile,
						ErrorCategory = error.IsWarning ? TaskErrorCategory.Warning : TaskErrorCategory.Error,
						Text = string.IsNullOrEmpty(error.Code) ? error.Message : $"{error.Code}: {error.Message}",
						Document = error.File,
						Line = Math.Max(0, error.Line - 1),
						Column = Math.Max(0, error.Column - 1)
					};
					task.Navigate += OnNavigate;
					provider.Tasks.Add(task);
				}
			}
			finally
			{
				provider.ResumeRefresh();
			}
			ShowIfNotEmpty();
		}

		/// <summary>Adds one non-navigable error per test failure.</summary>
		public void ReportTestFailures(string solutionName, IEnumerable<TestFailure> failures)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			provider.SuspendRefresh();
			try
			{
				foreach (var failure in failures)
				{
					provider.Tasks.Add(new ErrorTask
					{
						Category = TaskCategory.BuildCompile,
						ErrorCategory = TaskErrorCategory.Error,
						Text = $"{failure.TestName}: {failure.Message}",
						Document = solutionName
					});
				}
			}
			finally
			{
				provider.ResumeRefresh();
			}
			ShowIfNotEmpty();
		}

		public void Clear()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			provider.Tasks.Clear();
		}

		public void Dispose() => provider.Dispose();

		private void ShowIfNotEmpty()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (provider.Tasks.Count > 0)
				provider.Show();
		}

		private void OnNavigate(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var task = (ErrorTask)sender;
			if (string.IsNullOrEmpty(task.Document) || !File.Exists(task.Document))
				return;
			try
			{
				VsShellUtilities.OpenDocument(serviceProvider, task.Document, VSConstants.LOGVIEWID.Code_guid,
					out _, out _, out IVsWindowFrame frame, out IVsTextView textView);
				frame?.Show();
				textView?.SetCaretPos(task.Line, Math.Max(0, task.Column));
				textView?.CenterLines(task.Line, 1);
			}
			catch (Exception)
			{
			}
		}
	}
}
