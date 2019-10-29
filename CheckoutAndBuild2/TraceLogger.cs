using System;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2
{
	class TraceLogger : ConsoleLogger
	{
		public event EventHandler<BuildEventContext> ProjectFinished;

		private CancellationToken cancellationToken;

		public TraceLogger(LoggerVerbosity verbosity, CancellationToken cancellationToken = default (CancellationToken))
			: base(verbosity, s => Write(s, verbosity), ColorSet, ColorReset)
		{
			this.cancellationToken = cancellationToken;
		}

		public override void Initialize(IEventSource eventSource, int nodeCount)
		{
			base.Initialize(eventSource, nodeCount);
			if (eventSource != null)
				InitializeEvents(eventSource);
		}

		public override void Initialize(IEventSource eventSource)
		{
			base.Initialize(eventSource);
			InitializeEvents(eventSource);
		}

		private void InitializeEvents(IEventSource eventSource)
		{			
			eventSource.ErrorRaised += OnError;
			eventSource.TargetFinished += OnTargetFinished;
		}

		private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
		{
			if (e.TargetName == "Compile")
			{
				var handler = ProjectFinished;
				if (handler != null)
					handler(this, e.BuildEventContext);
			}
		}

		private static void ColorReset()
		{ }

		private static void ColorSet(ConsoleColor color)
		{ }

		private static void Write(string message, LoggerVerbosity verbosity)
		{
			if(verbosity != LoggerVerbosity.Quiet)
				Output.WriteLine(message);
		}

		private void OnError(object sender, BuildErrorEventArgs e)
		{			
			if(!cancellationToken.IsCancellationRequested)
				Output.WriteError(e.Message, e.File, e.LineNumber, e.ColumnNumber, e.HelpKeyword, TaskCategory.BuildCompile, e.ProjectFile, true);
		}
	}
}
