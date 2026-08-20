using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CheckoutAndBuild.Core.Model
{
	/// <summary>Parses .sln (format 12.x) and .slnx (XML) files and the contained project files without any IDE/MSBuild dependency.</summary>
	public static class SolutionParser
	{
		private const string SolutionFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
		private const string TestProjectTypeGuid = "{3AC096D0-A1C2-E12C-1390-A8335801FDAB}";

		private static readonly Regex projectLineRegex = new Regex(
			@"^Project\(""(?<type>\{[0-9A-Fa-f\-]+\})""\)\s*=\s*""(?<name>[^""]+)""\s*,\s*""(?<path>[^""]+)""\s*,\s*""(?<guid>\{[0-9A-Fa-f\-]+\})""",
			RegexOptions.Compiled);

		private static readonly string[] testPackagePrefixes = { "MSTest.TestFramework", "xunit", "nunit" };

		public static SolutionProjectModel Parse(string slnPath)
		{
			if (!File.Exists(slnPath))
				throw new FileNotFoundException("Solution file not found.", slnPath);

			if (slnPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
				return ParseSlnx(slnPath);

			var model = new SolutionProjectModel(slnPath);
			string solutionDir = model.SolutionFolder;
			bool inConfigSection = false;
			bool inProjectConfigSection = false;

			foreach (string rawLine in File.ReadAllLines(slnPath))
			{
				string line = rawLine.Trim();

				var match = projectLineRegex.Match(line);
				if (match.Success)
				{
					string typeGuid = match.Groups["type"].Value;
					if (string.Equals(typeGuid, SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
						continue;

					string relativePath = match.Groups["path"].Value;
					if (!relativePath.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
						continue;

					string fullPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
					var project = new ProjectInfo
					{
						Name = match.Groups["name"].Value,
						ProjectFilePath = fullPath,
						ProjectGuid = match.Groups["guid"].Value,
						ProjectTypeGuid = typeGuid
					};
					if (File.Exists(fullPath))
						ParseProjectFile(project);
					model.Projects.Add(project);
					continue;
				}

				if (line.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.OrdinalIgnoreCase))
				{
					inConfigSection = true;
					continue;
				}
				if (inConfigSection)
				{
					if (line.StartsWith("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
					{
						inConfigSection = false;
						continue;
					}
					int eq = line.IndexOf('=');
					if (eq > 0)
						model.SolutionConfigurations.Add(line.Substring(0, eq).Trim());
					continue;
				}

				if (line.StartsWith("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.OrdinalIgnoreCase))
				{
					inProjectConfigSection = true;
					continue;
				}
				if (inProjectConfigSection)
				{
					if (line.StartsWith("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
					{
						inProjectConfigSection = false;
						continue;
					}
					int eq = line.IndexOf('=');
					int guidEnd = line.IndexOf('}');
					if (eq < 0 || guidEnd < 0 || guidEnd > eq)
						continue;
					string key = line.Substring(0, eq).Trim();
					if (!key.EndsWith(".ActiveCfg", StringComparison.OrdinalIgnoreCase))
						continue;
					string guid = key.Substring(0, guidEnd + 1);
					string solutionConfig = key.Substring(guidEnd + 2, key.Length - guidEnd - 2 - ".ActiveCfg".Length);
					var project = model.Projects.FirstOrDefault(p => string.Equals(p.ProjectGuid, guid, StringComparison.OrdinalIgnoreCase));
					if (project != null)
						project.ProjectConfigurations[solutionConfig] = line.Substring(eq + 1).Trim();
				}
			}

			return model;
		}

		/// <summary>
		/// Parses the XML .slnx format: every &lt;Project Path="…"/&gt; (also inside &lt;Folder&gt; elements) becomes a project.
		/// The format has no project guids — synthetic ones are generated; configurations come from &lt;BuildType&gt; entries
		/// or default to Debug/Release.
		/// </summary>
		private static SolutionProjectModel ParseSlnx(string slnxPath)
		{
			var model = new SolutionProjectModel(slnxPath);
			string solutionDir = model.SolutionFolder;
			var doc = XDocument.Load(slnxPath);
			var root = doc.Root;
			if (root == null)
				return model;

			foreach (var element in root.Descendants().Where(e => e.Name.LocalName == "Project"))
			{
				string relativePath = (string)element.Attribute("Path");
				if (string.IsNullOrEmpty(relativePath) || !relativePath.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
					continue;
				string fullPath = Path.GetFullPath(Path.Combine(solutionDir,
					relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
				var project = new ProjectInfo
				{
					Name = Path.GetFileNameWithoutExtension(fullPath),
					ProjectFilePath = fullPath,
					ProjectGuid = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}"
				};
				if (File.Exists(fullPath))
					ParseProjectFile(project);
				project.ProjectTypeGuid = project.IsSdkStyle
					? "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"
					: "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
				model.Projects.Add(project);
			}

			var buildTypes = root.Descendants()
				.Where(e => e.Name.LocalName == "BuildType" && e.Parent?.Name.LocalName == "Configurations")
				.Select(e => (string)e.Attribute("Name"))
				.Where(name => !string.IsNullOrEmpty(name))
				.ToList();
			foreach (string name in buildTypes.Count > 0 ? buildTypes : new List<string> { "Debug", "Release" })
				model.SolutionConfigurations.Add($"{name}|Any CPU");

			return model;
		}

		private static void ParseProjectFile(ProjectInfo project)
		{
			var doc = XDocument.Load(project.ProjectFilePath);
			var root = doc.Root;
			if (root == null)
				return;

			string projectDir = Path.GetDirectoryName(project.ProjectFilePath);
			project.IsSdkStyle = root.Attribute("Sdk") != null;

			var propertyGroups = root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").ToList();
			var allProperties = propertyGroups.SelectMany(g => g.Elements()).ToList();

			string GetProperty(string name) =>
				allProperties.FirstOrDefault(e => e.Name.LocalName == name)?.Value;

			project.AssemblyName = GetProperty("AssemblyName")
				?? Path.GetFileNameWithoutExtension(project.ProjectFilePath);
			project.TargetFramework = GetProperty("TargetFramework")
				?? GetProperty("TargetFrameworks")?.Split(';')[0]
				?? GetProperty("TargetFrameworkVersion");

			string outputPath;
			string intermediatePath;
			if (project.IsSdkStyle)
			{
				string tfm = project.TargetFramework ?? string.Empty;
				outputPath = GetProperty("OutputPath") ?? Path.Combine("bin", "Debug", tfm);
				intermediatePath = GetProperty("IntermediateOutputPath") ?? Path.Combine("obj", "Debug", tfm);
			}
			else
			{
				var debugGroup = propertyGroups.FirstOrDefault(g =>
						((string)g.Attribute("Condition") ?? string.Empty).IndexOf("Debug|AnyCPU", StringComparison.OrdinalIgnoreCase) >= 0
						&& g.Elements().Any(e => e.Name.LocalName == "OutputPath"))
					?? propertyGroups.FirstOrDefault(g => g.Elements().Any(e => e.Name.LocalName == "OutputPath"));

				outputPath = debugGroup?.Elements().FirstOrDefault(e => e.Name.LocalName == "OutputPath")?.Value
					?? Path.Combine("bin", "Debug");
				intermediatePath = debugGroup?.Elements().FirstOrDefault(e => e.Name.LocalName == "IntermediateOutputPath")?.Value
					?? GetProperty("IntermediateOutputPath")
					?? Path.Combine("obj", "Debug");
			}

			project.OutputPath = Path.GetFullPath(Path.Combine(projectDir, outputPath.Replace('\\', Path.DirectorySeparatorChar)));
			project.IntermediateOutputPath = Path.GetFullPath(Path.Combine(projectDir, intermediatePath.Replace('\\', Path.DirectorySeparatorChar)));
			project.IsTestProject = DetectTestProject(root, GetProperty);
		}

		private static bool DetectTestProject(XElement root, Func<string, string> getProperty)
		{
			bool hasTestPackage = root.Descendants()
				.Where(e => e.Name.LocalName == "PackageReference")
				.Select(e => (string)e.Attribute("Include") ?? (string)e.Attribute("Update") ?? string.Empty)
				.Any(include => testPackagePrefixes.Any(p => include.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
			if (hasTestPackage)
				return true;

			bool hasQualityToolsReference = root.Descendants()
				.Where(e => e.Name.LocalName == "Reference")
				.Select(e => (string)e.Attribute("Include") ?? string.Empty)
				.Any(include => include.StartsWith("Microsoft.VisualStudio.QualityTools.UnitTestFramework", StringComparison.OrdinalIgnoreCase));
			if (hasQualityToolsReference)
				return true;

			if (getProperty("TestProjectType") != null)
				return true;
			string projectTypeGuids = getProperty("ProjectTypeGuids");
			return projectTypeGuids != null
				&& projectTypeGuids.IndexOf(TestProjectTypeGuid, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
