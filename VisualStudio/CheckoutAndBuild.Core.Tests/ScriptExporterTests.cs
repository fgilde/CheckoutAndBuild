#nullable disable
using System.Collections.Generic;
using System.IO;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Scripting;
using CheckoutAndBuild.Core.Services;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
	public class ScriptExporterTests
	{
		[Fact]
		public void Export_Batch_ContainsServicesInOrder_WithHeaders()
		{
			string fixture = Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
			var model = SolutionParser.Parse(fixture);
			var services = new IOperationService[] { new BuildService(), new CleanService(), new NugetRestoreService() };

			string script = ScriptExporter.Export(services, new[] { model }, null, ScriptExportType.Batch);

			Assert.StartsWith("@echo off", script);
			int clean = script.IndexOf("--- Clean ---");
			int nuget = script.IndexOf("--- Nuget Restore ---");
			int build = script.IndexOf("--- Build ---");
			Assert.True(clean >= 0 && nuget > clean && build > nuget, script);
			Assert.Contains("dotnet restore", script);
		}

		[Fact]
		public void Export_ExcludedProjects_AreSkipped()
		{
			string fixture = Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
			var model = SolutionParser.Parse(fixture);
			model.IsIncluded = false;

			string script = ScriptExporter.Export(new IOperationService[] { new NugetRestoreService() }, new[] { model }, null, ScriptExportType.Powershell);

			Assert.DoesNotContain("TestSolution.sln", script);
		}

		[Fact]
		public void Export_PluginScriptGenerators_WrapServiceScript()
		{
			string fixture = Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
			var model = SolutionParser.Parse(fixture);

			string script = ScriptExporter.Export(new IOperationService[] { new NugetRestoreService() },
				new[] { model }, null, ScriptExportType.Batch, new[] { new FakeGenerator() });

			int pre = script.IndexOf("echo PRE");
			int svc = script.IndexOf("dotnet restore");
			int post = script.IndexOf("echo POST");
			Assert.True(pre >= 0 && svc > pre && post > svc, script);
		}

		private sealed class FakeGenerator : IScriptGenerator
		{
			public string GeneratePreScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings, ScriptExportType scriptExportType) => "echo PRE";
			public string GeneratePostScriptCode(IOperationService service, IEnumerable<ISolutionProjectModel> solutions, IServiceSettings settings, ScriptExportType scriptExportType) => "echo POST";
		}
	}
}
