using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Build.Evaluation;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace CheckoutAndBuild2.Contracts
{
	public interface ISolutionProjectModel
	{
		Brush ImageBrush { get; set; }
		WorkingFolder ParentWorkingFolder { get; }
		OperationInfo CurrentOperation { get; set; }
		string ItemPath { get; }
		string ServerItem { get; }
		string ServerItemPath { get; }
		Geometry ImageGeometry { get; set; }
		bool IsIncluded { get; set; }
		int BuildPriority { get; set; }
		string SolutionFileName { get; }
		bool IsGitSourceControlled { get; }
		string SolutionFolder { get; }
		bool IsDelphiProject { get; }
		object ErrorContent { get; set; }
		ImageSource IconImage { get; }
		bool IsBusy { get; }
		void SetDefaultImageValues();
		IReadOnlyCollection<Project> GetUnitTestProjects();
		IReadOnlyCollection<Project> GetSolutionProjects();
		IEnumerable<string> BuildTargets { get; }
		IDictionary<string, string> BuildProperties { get; }
		void SetResult(ValidationResult result);
		void ResetProgress();
		void IncrementProgress();
	}
}