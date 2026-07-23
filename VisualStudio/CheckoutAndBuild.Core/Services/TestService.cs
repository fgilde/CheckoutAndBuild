using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.Core.Services
{
	public sealed class TestFailure
	{
		public string TestName { get; set; }
		public string Message { get; set; }
		public string StackTrace { get; set; }
	}

	/// <summary>Parsed result of one vstest run (from the .trx file).</summary>
	public sealed class TestRunResult
	{
		public int Total { get; set; }
		public int Passed { get; set; }
		public int Failed { get; set; }
		public IReadOnlyList<TestFailure> Failures { get; set; } = new TestFailure[0];
		public bool Success => Failed == 0;
	}

	/// <summary>
	/// Runs unit tests per solution via vstest.console.exe (located through vswhere, falling back
	/// to "dotnet vstest") and parses the .trx result. Replaces the old MSTest.exe runner.
	/// </summary>
	public class TestService : OperationServiceBase
	{
		public override Guid ServiceId => new Guid(ServiceIds.TestServiceId);
		public override int Order => ServicePriorities.UnitTestServicePriority;
		public override string OperationName => "Run Unit Tests";

		protected override async Task ExecuteCoreAsync(IReadOnlyList<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
		{
			var testSettings = GetSettings<UnitTestServiceSettings>(settings);
			foreach (var solution in solutionProjects)
			{
				await cancellation.WaitWhilePausedAsync().ConfigureAwait(false);
				if (IsCancelled(solution))
					continue;

				solution.CurrentOperation = Operations.UnitTestingIndeterminate;
				try
				{
					var assemblies = GetTestAssemblies(solution);
					if (assemblies.Count == 0)
						continue;

					var result = await RunTestsAsync(assemblies, solution.SolutionFolder, cancellation).ConfigureAwait(false);
					solution.SetResult(result);

					if (testSettings.CancelOnFailures && !result.Success)
					{
						Cancel();
						break;
					}
				}
				finally
				{
					solution.CurrentOperation = Operations.None;
				}
			}
		}

		public override string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType)
		{
			var builder = new StringBuilder();
			foreach (var model in models)
			{
				var assemblies = GetTestAssemblies(model);
				if (assemblies.Count == 0)
					continue;
				GetTestCommand(assemblies, out string exe, out string args);
				if (scriptExportType == ScriptExportType.Powershell && exe != "dotnet")
					builder.AppendLine($"& '{exe}' {args}");
				else if (exe == "dotnet")
					builder.AppendLine($"dotnet {args}");
				else
					builder.AppendLine($"\"{exe}\" {args}");
			}
			return builder.ToString();
		}

		private async Task<TestRunResult> RunTestsAsync(IReadOnlyList<string> assemblies, string workingDir, PausableCancellationTokenSource cancellation)
		{
			string resultsDir = Path.Combine(Path.GetTempPath(), "COAB", "TestResults", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(resultsDir);
			try
			{
				GetTestCommand(assemblies, out string exe, out string args);
				args += $" --ResultsDirectory:\"{resultsDir}\"";

				await ProcessRunner.RunAsync(exe, args, workingDir, cancellationToken: cancellation.Token).ConfigureAwait(false);

				string trx = Directory.GetFiles(resultsDir, "*.trx").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
				if (trx == null)
					return new TestRunResult { Failures = new[] { new TestFailure { TestName = "(run)", Message = "vstest produced no .trx result file." } }, Failed = 1 };
				return ParseTrx(trx);
			}
			finally
			{
				try { Directory.Delete(resultsDir, true); } catch { /* temp cleanup only */ }
			}
		}

		private static void GetTestCommand(IReadOnlyList<string> assemblies, out string exe, out string args)
		{
			string joined = string.Join(" ", assemblies.Select(a => $"\"{a}\""));
			string vstest = VsWhere.VsTestPath;
			if (vstest != null)
			{
				exe = vstest;
				args = $"{joined} --logger:trx";
			}
			else
			{
				exe = "dotnet";
				args = $"vstest {joined} --logger:trx";
			}
		}

		/// <summary>Output assemblies of all test projects of the solution that actually exist on disk.</summary>
		public static IReadOnlyList<string> GetTestAssemblies(ISolutionProjectModel solution)
		{
			var list = new List<string>();
			foreach (var project in (solution as SolutionProjectModel ?? SolutionParser.Parse(solution.ItemPath)).Projects)
			{
				if (!project.IsTestProject || string.IsNullOrEmpty(project.OutputPath))
					continue;
				string assemblyName = project.AssemblyName ?? Path.GetFileNameWithoutExtension(project.ProjectFilePath);
				string dll = Path.Combine(project.OutputPath, assemblyName + ".dll");
				if (File.Exists(dll))
					list.Add(dll);
			}
			return list;
		}

		/// <summary>Parses a vstest/MSTest .trx file (namespace-agnostic).</summary>
		public static TestRunResult ParseTrx(string trxPath)
		{
			var doc = XDocument.Load(trxPath);
			var counters = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Counters");

			var failures = doc.Descendants()
				.Where(e => e.Name.LocalName == "UnitTestResult"
							&& string.Equals((string)e.Attribute("outcome"), "Failed", StringComparison.OrdinalIgnoreCase))
				.Select(e =>
				{
					var errorInfo = e.Descendants().FirstOrDefault(x => x.Name.LocalName == "ErrorInfo");
					return new TestFailure
					{
						TestName = (string)e.Attribute("testName"),
						Message = errorInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "Message")?.Value,
						StackTrace = errorInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "StackTrace")?.Value
					};
				})
				.ToList();

			return new TestRunResult
			{
				Total = (int?)counters?.Attribute("total") ?? 0,
				Passed = (int?)counters?.Attribute("passed") ?? 0,
				Failed = (int?)counters?.Attribute("failed") ?? failures.Count,
				Failures = failures
			};
		}
	}
}
