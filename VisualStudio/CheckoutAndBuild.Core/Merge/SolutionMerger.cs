using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CheckoutAndBuild.Core.Model;

namespace CheckoutAndBuild.Core.Merge
{
	/// <summary>Merges multiple .sln files into one buildable solution (port of the legacy SolutionPacker, without CWDev.SLNTools).</summary>
	public static class SolutionMerger
	{
		private const string SolutionFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
		private const string DefaultConfig = "Debug|Any CPU";

		private sealed class MergedProject
		{
			public string Name;
			public string TypeGuid;
			public string Guid;
			public string AbsolutePath;
			public string FolderGuid;
			public IDictionary<string, string> Configurations;
		}

		/// <summary>Writes a merged .sln containing all projects of <paramref name="solutionPaths"/> and returns its path.</summary>
		public static string Merge(IEnumerable<string> solutionPaths, string outputSlnPath)
		{
			outputSlnPath = Path.GetFullPath(outputSlnPath);
			string outputDir = Path.GetDirectoryName(outputSlnPath);
			Directory.CreateDirectory(outputDir);

			var projects = new List<MergedProject>();
			var folders = new List<KeyValuePair<string, string>>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var seenGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var solutionConfigs = new List<string> { DefaultConfig, "Release|Any CPU" };

			foreach (string slnPath in solutionPaths)
			{
				var model = SolutionParser.Parse(slnPath);
				string folderGuid = System.Guid.NewGuid().ToString("B").ToUpperInvariant();
				folders.Add(new KeyValuePair<string, string>(
					Path.GetFileNameWithoutExtension(slnPath).Replace('.', '-'), folderGuid));

				foreach (string config in model.SolutionConfigurations)
					if (!solutionConfigs.Contains(config, StringComparer.OrdinalIgnoreCase))
						solutionConfigs.Add(config);

				foreach (var project in model.Projects)
				{
					if (!seenPaths.Add(project.ProjectFilePath))
						continue;
					string guid = project.ProjectGuid.ToUpperInvariant();
					if (!seenGuids.Add(guid))
					{
						guid = System.Guid.NewGuid().ToString("B").ToUpperInvariant(); // different project, colliding guid
						seenGuids.Add(guid);
					}
					projects.Add(new MergedProject
					{
						Name = project.Name,
						TypeGuid = project.ProjectTypeGuid,
						Guid = guid,
						AbsolutePath = project.ProjectFilePath,
						FolderGuid = folderGuid,
						Configurations = project.ProjectConfigurations
					});
				}
			}

			var sb = new StringBuilder();
			sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
			sb.AppendLine("# Visual Studio Version 17");

			foreach (var project in projects)
				sb.AppendLine($"Project(\"{project.TypeGuid}\") = \"{project.Name}\", \"{MakeRelative(outputDir, project.AbsolutePath)}\", \"{project.Guid}\"")
				  .AppendLine("EndProject");
			foreach (var folder in folders)
				sb.AppendLine($"Project(\"{SolutionFolderTypeGuid}\") = \"{folder.Key}\", \"{folder.Key}\", \"{folder.Value}\"")
				  .AppendLine("EndProject");

			sb.AppendLine("Global");
			sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
			foreach (string config in solutionConfigs)
				sb.AppendLine($"\t\t{config} = {config}");
			sb.AppendLine("\tEndGlobalSection");

			sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
			foreach (var project in projects)
				foreach (string config in solutionConfigs)
				{
					string projectConfig;
					if (!project.Configurations.TryGetValue(config, out projectConfig))
						projectConfig = DefaultConfig;
					sb.AppendLine($"\t\t{project.Guid}.{config}.ActiveCfg = {projectConfig}");
					sb.AppendLine($"\t\t{project.Guid}.{config}.Build.0 = {projectConfig}");
				}
			sb.AppendLine("\tEndGlobalSection");

			sb.AppendLine("\tGlobalSection(NestedProjects) = preSolution");
			foreach (var project in projects)
				sb.AppendLine($"\t\t{project.Guid} = {project.FolderGuid}");
			sb.AppendLine("\tEndGlobalSection");
			sb.AppendLine("EndGlobal");

			File.WriteAllText(outputSlnPath, sb.ToString());
			return outputSlnPath;
		}

		private static string MakeRelative(string fromDir, string toPath)
		{
			if (!string.Equals(Path.GetPathRoot(fromDir), Path.GetPathRoot(toPath), StringComparison.OrdinalIgnoreCase))
				return toPath;

			var fromUri = new Uri(fromDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
			string relative = Uri.UnescapeDataString(fromUri.MakeRelativeUri(new Uri(toPath)).ToString());
			return relative.Replace('/', '\\');
		}
	}
}
