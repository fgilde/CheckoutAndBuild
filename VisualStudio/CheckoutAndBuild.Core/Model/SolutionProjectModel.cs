using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CheckoutAndBuild.Core.Contracts;

namespace CheckoutAndBuild.Core.Model
{
	/// <summary>Parsed information about a single project inside a solution.</summary>
	public class ProjectInfo
	{
		public string Name { get; set; }
		/// <summary>Absolute path to the project file (.csproj etc.).</summary>
		public string ProjectFilePath { get; set; }
		public string ProjectGuid { get; set; }
		public string ProjectTypeGuid { get; set; }
		public bool IsSdkStyle { get; set; }
		/// <summary>Absolute output path (Debug configuration).</summary>
		public string OutputPath { get; set; }
		/// <summary>Absolute intermediate output path (Debug configuration).</summary>
		public string IntermediateOutputPath { get; set; }
		public string AssemblyName { get; set; }
		/// <summary>TargetFramework (SDK-style) or TargetFrameworkVersion (classic).</summary>
		public string TargetFramework { get; set; }
		public bool IsTestProject { get; set; }
		/// <summary>Maps a solution configuration (e.g. "Debug|Any CPU") to the project configuration (ActiveCfg) from the source .sln.</summary>
		public IDictionary<string, string> ProjectConfigurations { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>IDE-free implementation of <see cref="ISolutionProjectModel"/> backed by a parsed .sln file.</summary>
	public class SolutionProjectModel : NotificationObject, ISolutionProjectModel
	{
		private readonly List<ProjectInfo> projects = new List<ProjectInfo>();
		private readonly List<string> solutionConfigurations = new List<string>();
		private readonly List<string> buildTargets = new List<string> { "Build" };

		private OperationInfo currentOperation;
		private bool isIncluded = true;
		private int buildPriority;
		private object errorContent;
		private int progressCount;

		public SolutionProjectModel(string solutionPath)
		{
			if (string.IsNullOrEmpty(solutionPath))
				throw new ArgumentNullException(nameof(solutionPath));
			ItemPath = Path.GetFullPath(solutionPath);
		}

		/// <summary>Absolute path of the solution file.</summary>
		public string ItemPath { get; }

		public string SolutionFileName => Path.GetFileName(ItemPath);

		public string SolutionFolder => Path.GetDirectoryName(ItemPath);

		public bool IsDelphiProject => string.Equals(Path.GetExtension(ItemPath), ".groupproj", StringComparison.OrdinalIgnoreCase);

		public bool IsGitSourceControlled => GitRepositoryRoot != null;

		/// <summary>Root directory of the git repository containing the solution, or null.</summary>
		public string GitRepositoryRoot => FindGitRoot(SolutionFolder);

		public OperationInfo CurrentOperation
		{
			get { return currentOperation; }
			set
			{
				if (SetProperty(ref currentOperation, value))
					RaisePropertyChanged(nameof(IsBusy));
			}
		}

		public bool IsBusy => CurrentOperation != null;

		public bool IsIncluded
		{
			get { return isIncluded; }
			set { SetProperty(ref isIncluded, value); }
		}

		public int BuildPriority
		{
			get { return buildPriority; }
			set { SetProperty(ref buildPriority, value); }
		}

		public object ErrorContent
		{
			get { return errorContent; }
			set { SetProperty(ref errorContent, value); }
		}

		/// <summary>Result of the last operation, set via <see cref="SetResult"/>.</summary>
		public object Result { get; private set; }

		/// <summary>Detailed per-project information parsed from the project files.</summary>
		public IList<ProjectInfo> Projects => projects;

		/// <summary>Solution configurations, e.g. "Debug|Any CPU".</summary>
		public IList<string> SolutionConfigurations => solutionConfigurations;

		public IEnumerable<string> BuildTargets => buildTargets;

		/// <summary>Replaces the build targets; null/blank entries are dropped, empty falls back to the default "Build".</summary>
		public void SetBuildTargets(IEnumerable<string> targets)
		{
			buildTargets.Clear();
			if (targets != null)
				buildTargets.AddRange(targets.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
			if (buildTargets.Count == 0)
				buildTargets.Add("Build");
		}

		public IDictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();

		public IReadOnlyCollection<string> GetSolutionProjects()
		{
			return projects.Select(p => p.ProjectFilePath).ToList();
		}

		public IReadOnlyCollection<string> GetUnitTestProjects()
		{
			return projects.Where(p => p.IsTestProject).Select(p => p.ProjectFilePath).ToList();
		}

		public void SetResult(object result)
		{
			Result = result;
			if (result is Exception)
				ErrorContent = result;
		}

		public void ResetProgress()
		{
			progressCount = 0;
			if (CurrentOperation != null)
				CurrentOperation.Progress = 0;
		}

		public void IncrementProgress()
		{
			progressCount++;
			if (CurrentOperation != null && projects.Count > 0)
				CurrentOperation.SetProgress(projects.Count, progressCount);
		}

		private static string FindGitRoot(string directory)
		{
			var dir = new DirectoryInfo(directory);
			while (dir != null)
			{
				if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
					return dir.FullName;
				dir = dir.Parent;
			}
			return null;
		}

		public override string ToString()
		{
			return SolutionFileName;
		}
	}
}
