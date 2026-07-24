using System;
using Microsoft.Build.Framework;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public enum LogLevel
	{
		Disabled,
		Quiet,
		Minimal,
		Normal,
		Detailed,
		Diagnostic,
	}

	public static class LogLevelHelper
	{
		public static LogLevel ToLogLevel(this LoggerVerbosity verbosity)
		{
			switch (verbosity)
			{
				case LoggerVerbosity.Quiet:
					return LogLevel.Quiet;
				case LoggerVerbosity.Minimal:
					return LogLevel.Minimal;
				case LoggerVerbosity.Normal:
					return LogLevel.Normal;
				case LoggerVerbosity.Detailed:
					return LogLevel.Detailed;
				case LoggerVerbosity.Diagnostic:
					return LogLevel.Diagnostic;
				default:
					throw new ArgumentOutOfRangeException("verbosity");
			}
		}

		public static LoggerVerbosity ToLoggerVerbosity(this LogLevel level)
		{
			switch (level)
			{
				case LogLevel.Disabled:
					return LoggerVerbosity.Quiet;
				case LogLevel.Quiet:
					return LoggerVerbosity.Quiet;
				case LogLevel.Minimal:
					return LoggerVerbosity.Minimal;
				case LogLevel.Normal:
					return LoggerVerbosity.Normal;
				case LogLevel.Detailed:
					return LoggerVerbosity.Detailed;
				case LogLevel.Diagnostic:
					return LoggerVerbosity.Diagnostic;
				default:
					throw new ArgumentOutOfRangeException("level");
			}
		}
	}
}