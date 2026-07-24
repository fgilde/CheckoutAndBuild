using System.ComponentModel.Composition;
using System.IO;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.CP.Plugin
{
	[Export(typeof(IDefaultTestSettingsProvider))]
	public class CPTestSettingsProvider : IDefaultTestSettingsProvider
	{
		private const string defaultTestSettingsFileName = "CI.testsettings";
		public string GetTestSettingsFile(ISolutionProjectModel project, IServiceSettings settings)
		{
			var solutionFile = project.ItemPath;
			if (solutionFile.Contains(SolutionNames.Air))
				return "CP.Air.Test\\CI.testrunconfig";
			if(File.Exists(Path.Combine(project.SolutionFolder, defaultTestSettingsFileName) ))
				return defaultTestSettingsFileName;
			return string.Empty;
		}
	}
}