using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Pipeline;
using CheckoutAndBuild.Core.Services;
using CheckoutAndBuild.Core.Settings;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
	public class ServiceWiringTests : IDisposable
	{
		private readonly string tempDir = Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), "COAB.Tests", Guid.NewGuid().ToString("N"))).FullName;

		public void Dispose()
		{
			try { Directory.Delete(tempDir, true); } catch { }
		}

		private sealed class FakeSettings : IServiceSettings
		{
			private readonly Dictionary<Type, object> providers = new Dictionary<Type, object>();

			public void Add<T>(T provider) where T : ISettingsProviderClass => providers[typeof(T)] = provider;

			public bool RunPreScriptsAsync => false;
			public bool RunPostScriptsAsync => false;
			public string DelphiPath { get; set; } = "";
			public string PreBuildScriptPath => null;
			public string PostBuildScriptPath => null;
			public IDictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();
			public LoggerVerbosity LogLevel { get; set; } = LoggerVerbosity.Minimal;

			public T GetSettingsFromProvider<T>() where T : ISettingsProviderClass, new()
				=> providers.TryGetValue(typeof(T), out object value) ? (T)value : new T();

			public T GetSettingsFromProvider<T>(ISolutionProjectModel solutionProject) where T : ISettingsProviderClass, new()
				=> GetSettingsFromProvider<T>();
		}

		private sealed class FakeProject : ISolutionProjectModel
		{
			public OperationInfo CurrentOperation { get; set; }
			public string ItemPath { get; set; }
			public bool IsIncluded { get; set; } = true;
			public int BuildPriority { get; set; }
			public string SolutionFileName => Path.GetFileName(ItemPath);
			public bool IsGitSourceControlled => false;
			public string SolutionFolder => Path.GetDirectoryName(ItemPath);
			public bool IsDelphiProject { get; set; }
			public object ErrorContent { get; set; }
			public bool IsBusy => false;
			public IReadOnlyCollection<string> GetUnitTestProjects() => new string[0];
			public IReadOnlyCollection<string> GetSolutionProjects() => new string[0];
			public IEnumerable<string> BuildTargets => null;
			public IDictionary<string, string> BuildProperties => null;
			public void SetResult(object result) { }
			public void ResetProgress() { }
			public void IncrementProgress() { }
		}

		private sealed class NoopService : IOperationService
		{
			public Guid ServiceId => new Guid(ServiceIds.CleanServiceId);
			public int Order => 1;
			public string OperationName => "Noop";
			public Task ExecuteAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation) => Task.CompletedTask;
			public void Cancel() { }
			public void Cancel(ISolutionProjectModel solutionProject) { }
			public bool IsCancelled(ISolutionProjectModel solutionProject) => false;
			public bool AllowScriptExport => false;
			public ScriptExportType[] SupportedScriptExportTypes => new ScriptExportType[0];
			public string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType) => string.Empty;
		}

		[Fact]
		public async Task PipelineRunner_RunsPreAndPostServiceScripts_WithServiceAndSolutionArgs()
		{
			string marker = Path.Combine(tempDir, "marker.txt");
			string script = Path.Combine(tempDir, "hook.bat");
			File.WriteAllText(script, $"@echo %1 %2>> \"{marker}\"\r\n@exit /b 0");

			var settings = new FakeSettings();
			settings.Add(new MiscellaneousSettings { PreServiceScriptFile = script, PostServiceScriptFile = script });
			var project = new FakeProject { ItemPath = Path.Combine(tempDir, "My.sln") };

			await new PipelineRunner().RunAsync(new[] { (ISolutionProjectModel)project }, new[] { new NoopService() },
				new PipelineContext { Settings = settings }, new PausableCancellationTokenSource());

			string[] lines = File.ReadAllLines(marker);
			Assert.Equal(2, lines.Length); // pre + post
			Assert.All(lines, line => Assert.Contains("\"Noop\"", line));
			Assert.All(lines, line => Assert.Contains("My.sln", line));
		}

		[Fact]
		public async Task PipelineRunner_ReportsFailingServiceScript_AndContinues()
		{
			string script = Path.Combine(tempDir, "fail.bat");
			File.WriteAllText(script, "@exit /b 3");

			var settings = new FakeSettings();
			settings.Add(new MiscellaneousSettings { PreServiceScriptFile = script });
			var errors = new List<string>();
			var progress = new SynchronousProgress(p => { if (p.Error != null) errors.Add(p.Error); });

			await new PipelineRunner().RunAsync(new ISolutionProjectModel[] { new FakeProject { ItemPath = Path.Combine(tempDir, "My.sln") } },
				new[] { new NoopService() },
				new PipelineContext { Settings = settings, Progress = progress },
				new PausableCancellationTokenSource());

			Assert.Contains(errors, e => e.Contains("exit code 3"));
		}

		private sealed class SynchronousProgress : IProgress<PipelineProgress>
		{
			private readonly Action<PipelineProgress> handler;
			public SynchronousProgress(Action<PipelineProgress> handler) => this.handler = handler;
			public void Report(PipelineProgress value) => handler(value);
		}

		[Fact]
		public void BuildService_DelphiProject_BuildsViaRsvarsAndMsbuild()
		{
			var settings = new FakeSettings { DelphiPath = @"C:\Delphi\22.0" };
			var model = new FakeProject { ItemPath = Path.Combine(tempDir, "App.dproj"), IsDelphiProject = true };

			string script = new BuildService().GetScript(new[] { model }, settings, ScriptExportType.Batch);

			Assert.Contains(@"C:\Delphi\22.0\bin\rsvars.bat", script);
			Assert.Contains("msbuild", script);
			Assert.Contains("App.dproj", script);
		}

		[Fact]
		public void BuildService_VerbosityFollowsLogLevel()
		{
			var settings = new FakeSettings { LogLevel = LoggerVerbosity.Detailed };
			var model = new FakeProject { ItemPath = Path.Combine(tempDir, "My.sln") };

			string script = new BuildService().GetScript(new[] { model }, settings, ScriptExportType.Batch);

			Assert.True(script.Contains("/v:d") || script.Contains("-v d"), $"verbosity flag missing in: {script}");
		}

		[Fact]
		public void NugetRestoreService_ReinstallUsesForceRestore_WhenNoNugetExe()
		{
			var settings = new FakeSettings();
			settings.Add(new NugetServiceSettings { NugetAction = NugetAction.Reinstall });
			var model = new FakeProject { ItemPath = Path.Combine(tempDir, "My.sln") };

			string script = new NugetRestoreService().GetScript(new[] { model }, settings, ScriptExportType.Batch);

			Assert.Contains("--force", script);
		}
	}
}
