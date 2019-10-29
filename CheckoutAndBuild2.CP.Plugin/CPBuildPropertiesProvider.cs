using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;

namespace CheckoutAndBuild2.CP.Plugin
{
	[Export(typeof(IProjectBuildPropertiesProvider))]
	public class CPBuildPropertiesProvider : IProjectBuildPropertiesProvider
	{
		public IDictionary<string, string> GetDefaultBuildProperties(ISolutionProjectModel project, IServiceSettings settings)
		{
		    if (settings.GetSettingsFromProvider<CPSettings>().SetupDelphiMagic)
		    {
		        if (project.SolutionFileName.ToLower().Contains(SolutionNames.PlannerEmbedded.ToLower()))
		            return GetPlannerEmbeddedBuildProperties(project);
		    }
		    return new Dictionary<string, string>();
		}

		private static string GetEnvDirIfExisting(string varName)
		{
			var dirs = new List<string>
			{
				Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.Process),
				Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User),
				Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.Machine)
			};
			return dirs.FirstOrDefault(s => !string.IsNullOrEmpty(s) && Directory.Exists(s));
		}

		private static IDictionary<string, string> GetPlannerEmbeddedBuildProperties(ISolutionProjectModel project)
		{
			var dir = new DirectoryInfo(project.SolutionFolder).Parent.Parent;
			string modulesPath = GetEnvDirIfExisting("DEVXE2MOD");
			if (string.IsNullOrEmpty(modulesPath) || !Directory.Exists(modulesPath))
			{
				modulesPath = Path.Combine(dir.FullName, "Modules");
				if (Directory.Exists(modulesPath))
					Environment.SetEnvironmentVariable("DEVXE2MOD", modulesPath);
			}

			var result = new Dictionary<string, string>
			{
				//{"PLATFORM", "Win32"},
				{"DEVXE2MOD", modulesPath}
			};

			const int lastDxvcl = 3;
			for (int i = 2; i <= lastDxvcl; i++)
			{
				string dxvcl = GetEnvDirIfExisting($"DXVCL_V{i}");
				if (string.IsNullOrEmpty(dxvcl) || !Directory.Exists(dxvcl))
				{
					dxvcl = Path.Combine(modulesPath, $"dxvcl_v{i}");
					if (Directory.Exists(modulesPath))
						Environment.SetEnvironmentVariable($"DXVCL_V{i}", dxvcl);
				}
				result.Add($"DXVCL_V{i}", dxvcl);
			}

			return result;
		}
	}
}