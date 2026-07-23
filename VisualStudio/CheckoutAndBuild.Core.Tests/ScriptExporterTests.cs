#nullable disable
using System.IO;
using CheckoutAndBuild.Core.Contracts.Service;
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
	}
}
