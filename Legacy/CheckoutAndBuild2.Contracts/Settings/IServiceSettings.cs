using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace CheckoutAndBuild2.Contracts.Settings
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
}