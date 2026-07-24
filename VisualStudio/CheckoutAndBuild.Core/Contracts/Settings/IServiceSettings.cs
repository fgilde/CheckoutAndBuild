using System.Collections.Generic;

namespace CheckoutAndBuild.Core.Contracts.Settings
{
	public interface IServiceSettings
	{
		bool RunPreScriptsAsync { get; }
		bool RunPostScriptsAsync { get; }
		string DelphiPath { get; }
		string PreBuildScriptPath { get; }
		string PostBuildScriptPath { get; }
		IDictionary<string, string> BuildProperties { get; }
		LoggerVerbosity LogLevel { get; }
		T GetSettingsFromProvider<T>() where T : ISettingsProviderClass, new();
		T GetSettingsFromProvider<T>(ISolutionProjectModel solutionProject) where T : ISettingsProviderClass, new();
	}

	/// <summary>
	/// MSBuild logger verbosity (mirrors Microsoft.Build.Framework.LoggerVerbosity,
	/// kept local so Contracts stays free of MSBuild assemblies; maps to msbuild.exe /v:).
	/// </summary>
	public enum LoggerVerbosity
	{
		Quiet,
		Minimal,
		Normal,
		Detailed,
		Diagnostic
	}
}
