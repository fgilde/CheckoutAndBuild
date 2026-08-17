using System.Collections.Generic;

namespace CheckoutAndBuild.Core.Contracts
{
	public interface ISolutionProjectModel
	{
		OperationInfo CurrentOperation { get; set; }
		string ItemPath { get; }
		bool IsIncluded { get; set; }
		int BuildPriority { get; set; }
		string SolutionFileName { get; }
		bool IsGitSourceControlled { get; }
		string SolutionFolder { get; }
		bool IsDelphiProject { get; }
		object ErrorContent { get; set; }
		bool IsBusy { get; }
		IReadOnlyCollection<string> GetUnitTestProjects();
		IReadOnlyCollection<string> GetSolutionProjects();
		IEnumerable<string> BuildTargets { get; }
		IDictionary<string, string> BuildProperties { get; }
		void SetResult(object result);
		void ResetProgress();
		void IncrementProgress();
	}
}
