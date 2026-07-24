using System;
using CheckoutAndBuild.Core.Contracts.Settings;

namespace CheckoutAndBuild.Core.Execution
{
	/// <summary>
	/// Process-wide log sink for pipeline/service messages. The host subscribes once and
	/// forwards to its output window, filtering by the configured verbosity.
	/// ponytail: static event, one host per process; swap for injected logger if Core ever runs multi-tenant.
	/// </summary>
	public static class CoabLog
	{
		public static event Action<LoggerVerbosity, string> MessageLogged;

		public static void Write(LoggerVerbosity level, string message) => MessageLogged?.Invoke(level, message);

		/// <summary>Milestones (service started/finished, exported file, merged solution).</summary>
		public static void Info(string message) => Write(LoggerVerbosity.Normal, message);

		/// <summary>Raw tool output lines (msbuild/vstest/nuget/git).</summary>
		public static void Detail(string message) => Write(LoggerVerbosity.Detailed, message);

		public static void Error(string message) => Write(LoggerVerbosity.Minimal, message);
	}
}
