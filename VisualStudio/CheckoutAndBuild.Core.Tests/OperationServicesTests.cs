#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Services;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
	public sealed class TempDir : IDisposable
	{
		public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "COAB.Tests", Guid.NewGuid().ToString("N"));
		public TempDir() { Directory.CreateDirectory(Path); }
		public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
	}

	public class CleanServiceTests
	{
		[Fact]
		public async Task Clean_DeletesOutputAndIntermediateAndCustomPaths()
		{
			using (var temp = new TempDir())
			{
				string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
				string slnDir = Path.Combine(temp.Path, "sln");
				CopyDirectory(Path.GetDirectoryName(fixture), slnDir);
				var model = SolutionParser.Parse(Path.Combine(slnDir, "TestSolution.sln"));

				var project = model.Projects[0];
				Directory.CreateDirectory(project.OutputPath);
				File.WriteAllText(Path.Combine(project.OutputPath, "x.dll"), "x");
				string custom = Path.Combine(slnDir, "customOut");
				Directory.CreateDirectory(custom);

				var settings = new FakeServiceSettings();
				settings.Provide(new CheckoutAndBuild.Core.Settings.CleanServiceSettings { CustomCleanPathes = new[] { "customOut" } });

				await new CleanService().ExecuteAsync(new[] { model }, settings, new PausableCancellationTokenSource());

				Assert.False(Directory.Exists(project.OutputPath));
				Assert.False(Directory.Exists(custom));
			}
		}

		[Fact]
		public void GetScript_Batch_ContainsRmdir()
		{
			string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
			var model = SolutionParser.Parse(fixture);
			string script = new CleanService().GetScript(new[] { model }, null, ScriptExportType.Batch);
			Assert.Contains("rmdir /s /q", script);
		}

		internal static void CopyDirectory(string source, string target)
		{
			Directory.CreateDirectory(target);
			foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
			{
				string dest = Path.Combine(target, file.Substring(source.Length + 1));
				Directory.CreateDirectory(Path.GetDirectoryName(dest));
				File.Copy(file, dest);
			}
		}
	}

	public class GitCheckoutServiceTests
	{
		[Fact]
		public void GroupByRepositoryRoot_DeduplicatesSameRepo()
		{
			var a = new SolutionProjectModel(@"C:\repo\a\a.sln");
			var b = new SolutionProjectModel(@"C:\repo\b\b.sln");
			var groups = GitCheckoutService.GroupByRepositoryRoot(
				new ISolutionProjectModel[] { new GitModel(a), new GitModel(b) }, dir => @"C:\repo");
			Assert.Single(groups);
			Assert.Equal(2, groups.Values.Single().Count);
		}

		[Fact]
		public void GetScript_EmitsOnePullPerRepository()
		{
			var a = new SolutionProjectModel(Path.Combine(RepoRoot(), "CheckoutAndBuild2.sln"));
			string script = new GitCheckoutService().GetScript(new ISolutionProjectModel[] { new GitModel(a) }, null, ScriptExportType.Batch);
			Assert.Contains("git -C", script);
			Assert.Contains("pull", script);
		}

		private static string RepoRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
				dir = dir.Parent;
			return dir?.FullName ?? AppContext.BaseDirectory;
		}

		/// <summary>Wraps a model and forces IsGitSourceControlled = true.</summary>
		private sealed class GitModel : ISolutionProjectModel
		{
			private readonly SolutionProjectModel inner;
			public GitModel(SolutionProjectModel inner) { this.inner = inner; }
			public OperationInfo CurrentOperation { get => inner.CurrentOperation; set => inner.CurrentOperation = value; }
			public string ItemPath => inner.ItemPath;
			public bool IsIncluded { get => inner.IsIncluded; set => inner.IsIncluded = value; }
			public int BuildPriority { get => inner.BuildPriority; set => inner.BuildPriority = value; }
			public string SolutionFileName => inner.SolutionFileName;
			public bool IsGitSourceControlled => true;
			public string SolutionFolder => inner.SolutionFolder;
			public bool IsDelphiProject => inner.IsDelphiProject;
			public object ErrorContent { get => inner.ErrorContent; set => inner.ErrorContent = value; }
			public bool IsBusy => inner.IsBusy;
			public IReadOnlyCollection<string> GetUnitTestProjects() => inner.GetUnitTestProjects();
			public IReadOnlyCollection<string> GetSolutionProjects() => inner.GetSolutionProjects();
			public IEnumerable<string> BuildTargets => inner.BuildTargets;
			public IDictionary<string, string> BuildProperties => inner.BuildProperties;
			public void SetResult(object result) => inner.SetResult(result);
			public void ResetProgress() => inner.ResetProgress();
			public void IncrementProgress() => inner.IncrementProgress();
		}
	}

	public class NugetRestoreServiceTests
	{
		[Fact]
		public void GetScript_UsesDotnetRestore_WhenNoNugetExeConfigured()
		{
			string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
			var model = SolutionParser.Parse(fixture);
			string script = new NugetRestoreService().GetScript(new[] { model }, null, ScriptExportType.Batch);
			Assert.Contains("dotnet restore", script);
			Assert.Contains("TestSolution.sln", script);
		}
	}

	public class BuildServiceTests
	{
		[Fact]
		public async Task Build_MiniSolution_Succeeds_AndOutputExists()
		{
			using (var temp = new TempDir())
			{
				var model = SolutionParser.Parse(CreateMiniSolution(temp.Path, brokenCode: false));

				var result = await new BuildService().BuildSolutionAsync(model, null, new PausableCancellationTokenSource());

				Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.ToString())));
				string dll = Path.Combine(model.Projects[0].OutputPath, "MiniLib.dll");
				Assert.True(File.Exists(dll), $"missing: {dll}");
			}
		}

		[Fact]
		public async Task Build_BrokenCode_ReportsParsedCsError()
		{
			using (var temp = new TempDir())
			{
				var model = SolutionParser.Parse(CreateMiniSolution(temp.Path, brokenCode: true));

				var result = await new BuildService().BuildSolutionAsync(model, null, new PausableCancellationTokenSource());

				Assert.False(result.Success);
				var error = result.Errors.FirstOrDefault(e => !e.IsWarning);
				Assert.NotNull(error);
				Assert.StartsWith("CS", error.Code);
				Assert.True(error.Line > 0);
			}
		}

		internal static string CreateMiniSolution(string dir, bool brokenCode)
		{
			string projDir = Path.Combine(dir, "MiniLib");
			Directory.CreateDirectory(projDir);
			File.WriteAllText(Path.Combine(projDir, "MiniLib.csproj"),
				"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>netstandard2.0</TargetFramework>\n  </PropertyGroup>\n</Project>");
			File.WriteAllText(Path.Combine(projDir, "Class1.cs"),
				brokenCode ? "public class Class1 { public void M() { int x = } }" : "public class Class1 { }");

			string projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
			string sln = Path.Combine(dir, "Mini.sln");
			File.WriteAllText(sln,
				"Microsoft Visual Studio Solution File, Format Version 12.00\n" +
				$"Project(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"MiniLib\", \"MiniLib\\MiniLib.csproj\", \"{projectGuid}\"\nEndProject\n" +
				"Global\n\tGlobalSection(SolutionConfigurationPlatforms) = preSolution\n\t\tDebug|Any CPU = Debug|Any CPU\n\tEndGlobalSection\n" +
				$"\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\n\t\t{projectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\n\t\t{projectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU\n\tEndGlobalSection\nEndGlobal\n");
			return sln;
		}
	}

	public class TestServiceTests
	{
		[Fact]
		public void ParseTrx_ReadsCountersAndFailures()
		{
			string trx = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".trx");
			File.WriteAllText(trx, SampleTrx);
			try
			{
				var result = TestService.ParseTrx(trx);
				Assert.Equal(3, result.Total);
				Assert.Equal(2, result.Passed);
				Assert.Equal(1, result.Failed);
				Assert.False(result.Success);
				var failure = Assert.Single(result.Failures);
				Assert.Equal("FailingTest", failure.TestName);
				Assert.Contains("expected 1 but was 2", failure.Message);
			}
			finally
			{
				File.Delete(trx);
			}
		}

		[Fact]
		public void GetTestAssemblies_FindsBuiltTestProjectOutput()
		{
			string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
			var model = SolutionParser.Parse(fixture);
			Assert.Empty(TestService.GetTestAssemblies(model));
		}

		private const string SampleTrx = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestRun id=""aa1e33f2-b02f-4123-9a53-c46fd505d1cb"" xmlns=""http://microsoft.com/schemas/VisualStudio/TeamTest/2010"">
  <Results>
    <UnitTestResult testName=""PassingTest1"" outcome=""Passed"" />
    <UnitTestResult testName=""PassingTest2"" outcome=""Passed"" />
    <UnitTestResult testName=""FailingTest"" outcome=""Failed"">
      <Output>
        <ErrorInfo>
          <Message>Assert.Equal() Failure: expected 1 but was 2</Message>
          <StackTrace>at Tests.FailingTest()</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
  </Results>
  <ResultSummary outcome=""Failed"">
    <Counters total=""3"" executed=""3"" passed=""2"" failed=""1"" />
  </ResultSummary>
</TestRun>";
	}

	/// <summary>Minimal IServiceSettings backed by a type->instance dictionary.</summary>
	public sealed class FakeServiceSettings : CheckoutAndBuild.Core.Contracts.Settings.IServiceSettings
	{
		private readonly Dictionary<Type, object> providers = new Dictionary<Type, object>();
		public void Provide<T>(T instance) => providers[typeof(T)] = instance;

		public bool RunPreScriptsAsync => false;
		public bool RunPostScriptsAsync => false;
		public string DelphiPath => null;
		public string PreBuildScriptPath => null;
		public string PostBuildScriptPath => null;
		public IDictionary<string, string> BuildProperties => new Dictionary<string, string>();
		public CheckoutAndBuild.Core.Contracts.Settings.LoggerVerbosity LogLevel =>
			CheckoutAndBuild.Core.Contracts.Settings.LoggerVerbosity.Minimal;

		public T GetSettingsFromProvider<T>() where T : CheckoutAndBuild.Core.Contracts.ISettingsProviderClass, new()
			=> providers.TryGetValue(typeof(T), out object value) ? (T)value : new T();
		public T GetSettingsFromProvider<T>(ISolutionProjectModel projectSpecific) where T : CheckoutAndBuild.Core.Contracts.ISettingsProviderClass, new()
			=> GetSettingsFromProvider<T>();
	}
}
