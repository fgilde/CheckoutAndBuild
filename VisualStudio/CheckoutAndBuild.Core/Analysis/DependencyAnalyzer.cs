using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CheckoutAndBuild.Core.Model;

namespace CheckoutAndBuild.Core.Analysis
{
	/// <summary>
	/// Suggests build priorities by scanning cross-solution dependencies: solution A depends on
	/// solution B when a project of A references an assembly that a project of B produces
	/// (Reference / ProjectReference / PackageReference names matched against assembly names).
	/// Priority = dependency depth, so referenced solutions build first.
	/// </summary>
	public static class DependencyAnalyzer
	{
		/// <summary>Suggested priority (0 = build first) per solution path. Solutions without cross-references get 0.</summary>
		public static IReadOnlyDictionary<string, int> SuggestBuildPriorities(IReadOnlyList<SolutionProjectModel> solutions)
		{
			var produced = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
			var referenced = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

			foreach (var solution in solutions)
			{
				var producedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var referencedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var project in solution.Projects)
				{
					producedNames.Add(project.AssemblyName ?? Path.GetFileNameWithoutExtension(project.ProjectFilePath));
					foreach (string name in GetReferencedNames(project.ProjectFilePath))
						referencedNames.Add(name);
				}
				produced[solution.ItemPath] = producedNames;
				referenced[solution.ItemPath] = referencedNames;
			}

			var dependsOn = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var solution in solutions)
			{
				dependsOn[solution.ItemPath] = solutions
					.Where(other => !string.Equals(other.ItemPath, solution.ItemPath, StringComparison.OrdinalIgnoreCase))
					.Where(other => referenced[solution.ItemPath].Overlaps(produced[other.ItemPath]))
					.Select(other => other.ItemPath)
					.ToList();
			}

			var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			int Depth(string path)
			{
				if (depth.TryGetValue(path, out int known))
					return known;
				if (!onStack.Add(path))
					return 0;
				int value = 0;
				foreach (string dependency in dependsOn[path])
					value = Math.Max(value, Depth(dependency) + 1);
				onStack.Remove(path);
				depth[path] = value;
				return value;
			}

			foreach (var solution in solutions)
				Depth(solution.ItemPath);
			return depth;
		}

		/// <summary>Assembly/package names a project references (namespace-agnostic csproj parsing; unreadable files yield nothing).</summary>
		public static IEnumerable<string> GetReferencedNames(string projectFilePath)
		{
			XDocument doc;
			try
			{
				if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath))
					yield break;
				doc = XDocument.Load(projectFilePath);
			}
			catch (Exception)
			{
				yield break;
			}

			foreach (var element in doc.Descendants())
			{
				string include = (string)element.Attribute("Include");
				if (string.IsNullOrEmpty(include))
					continue;
				switch (element.Name.LocalName)
				{
					case "Reference":
					case "PackageReference":
						yield return include.Split(',')[0].Trim();
						break;
					case "ProjectReference":
						yield return Path.GetFileNameWithoutExtension(include);
						break;
				}
			}
		}
	}
}
