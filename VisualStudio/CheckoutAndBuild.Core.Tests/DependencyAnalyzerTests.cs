using System;
using System.Collections.Generic;
using System.IO;
using CheckoutAndBuild.Core.Analysis;
using CheckoutAndBuild.Core.Model;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
	public class DependencyAnalyzerTests : IDisposable
	{
		private readonly string tempDir = Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), "COAB.Tests", Guid.NewGuid().ToString("N"))).FullName;

		public void Dispose()
		{
			try { Directory.Delete(tempDir, true); } catch { }
		}

		private SolutionProjectModel CreateSolution(string name, string assemblyName, params string[] references)
		{
			string dir = Directory.CreateDirectory(Path.Combine(tempDir, name)).FullName;
			var referenceXml = string.Join(Environment.NewLine,
				Array.ConvertAll(references, r => $"    <Reference Include=\"{r}\" />"));
			File.WriteAllText(Path.Combine(dir, name + ".csproj"), $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>{assemblyName}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
{referenceXml}
  </ItemGroup>
</Project>");
			string guid = Guid.NewGuid().ToString("B").ToUpperInvariant();
			File.WriteAllText(Path.Combine(dir, name + ".sln"), $@"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}"") = ""{name}"", ""{name}.csproj"", ""{guid}""
EndProject
Global
EndGlobal
");
			return SolutionParser.Parse(Path.Combine(dir, name + ".sln"));
		}

		[Fact]
		public void SuggestBuildPriorities_ReferencedSolutionBuildsFirst()
		{
			var core = CreateSolution("CoreLib", "My.Core");
			var app = CreateSolution("App", "My.App", "My.Core");
			var ui = CreateSolution("Ui", "My.Ui", "My.App, Version=1.0.0.0, Culture=neutral");

			var priorities = DependencyAnalyzer.SuggestBuildPriorities(new List<SolutionProjectModel> { ui, app, core });

			Assert.Equal(0, priorities[core.ItemPath]);
			Assert.Equal(1, priorities[app.ItemPath]);
			Assert.Equal(2, priorities[ui.ItemPath]);
		}

		[Fact]
		public void SuggestBuildPriorities_IndependentSolutionsShareLevelZero()
		{
			var a = CreateSolution("A", "Lib.A");
			var b = CreateSolution("B", "Lib.B");

			var priorities = DependencyAnalyzer.SuggestBuildPriorities(new List<SolutionProjectModel> { a, b });

			Assert.Equal(0, priorities[a.ItemPath]);
			Assert.Equal(0, priorities[b.ItemPath]);
		}

		[Fact]
		public void SuggestBuildPriorities_CycleDoesNotHang()
		{
			var a = CreateSolution("CycA", "Cyc.A", "Cyc.B");
			var b = CreateSolution("CycB", "Cyc.B", "Cyc.A");

			var priorities = DependencyAnalyzer.SuggestBuildPriorities(new List<SolutionProjectModel> { a, b });

			Assert.Equal(2, priorities.Count);
		}
	}
}
