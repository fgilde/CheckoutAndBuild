using System.Text.RegularExpressions;
using CheckoutAndBuild.Core.Execution;
using CheckoutAndBuild.Core.Merge;
using CheckoutAndBuild.Core.Model;

namespace CheckoutAndBuild.Core.Tests;

public class SolutionMergerTests : IDisposable
{
    private static readonly string FixtureDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Merge");

    private static readonly string SlnA = Path.Combine(FixtureDir, "SolutionA", "A.sln");
    private static readonly string SlnB = Path.Combine(FixtureDir, "SolutionB", "B.sln");

    // must live on the same drive as the fixtures: relative project paths cannot cross drives (CI keeps the repo on D:)
    private readonly string outputDir =
        Path.Combine(AppContext.BaseDirectory, "coab-merge-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(outputDir, recursive: true); } catch { /* best effort */ }
    }

    private string MergeFixtures()
    {
        Directory.CreateDirectory(outputDir);
        return SolutionMerger.Merge(new[] { SlnA, SlnB }, Path.Combine(outputDir, "Build.sln"));
    }

    private static List<(string Name, string Path, string Guid)> GetProjectLines(string slnText)
    {
        return Regex.Matches(slnText,
                @"^Project\(""\{9A19103F-16F7-4668-BE54-9A1E7A4F7556\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+)"",\s*""(\{[^}]+\})""",
                RegexOptions.Multiline)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value))
            .ToList();
    }

    [Fact]
    public void Merge_ContainsEachProjectExactlyOnce()
    {
        string merged = MergeFixtures();
        var projects = GetProjectLines(File.ReadAllText(merged));

        Assert.Equal(new[] { "LibA", "LibB", "Shared" }, projects.Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public void Merge_GuidCollision_SecondProjectGetsNewGuid_AndGuidsAreUnique()
    {
        string merged = MergeFixtures();
        var projects = GetProjectLines(File.ReadAllText(merged));

        Assert.Equal("{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}",
            projects.Single(p => p.Name == "LibA").Guid);
        Assert.NotEqual("{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}",
            projects.Single(p => p.Name == "LibB").Guid);
        Assert.Equal(projects.Count, projects.Select(p => p.Guid.ToUpperInvariant()).Distinct().Count());
    }

    [Fact]
    public void Merge_ProjectPaths_AreRelativeAndResolveFromOutputSln()
    {
        string merged = MergeFixtures();
        var model = SolutionParser.Parse(merged);

        Assert.Equal(3, model.Projects.Count);
        Assert.All(model.GetSolutionProjects(), p => Assert.True(File.Exists(p), $"missing: {p}"));

        var rawPaths = GetProjectLines(File.ReadAllText(merged)).Select(p => p.Path);
        Assert.All(rawPaths, p => Assert.False(Path.IsPathRooted(p), $"not relative: {p}"));
    }

    [Fact]
    public void Merge_SolutionConfigurations_AreUnioned_WithFallbackMapping()
    {
        string merged = MergeFixtures();
        string text = File.ReadAllText(merged);
        var model = SolutionParser.Parse(merged);

        Assert.Contains("Debug|Any CPU", model.SolutionConfigurations);
        Assert.Contains("Release|Any CPU", model.SolutionConfigurations);
        Assert.Contains("Staging|Any CPU", model.SolutionConfigurations);

        // LibA's source solution has no Staging config -> falls back to Debug|Any CPU
        Assert.Contains("{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}.Staging|Any CPU.ActiveCfg = Debug|Any CPU", text);
        // LibB keeps its source mapping Staging -> Release (under its new guid)
        string libBGuid = GetProjectLines(text).Single(p => p.Name == "LibB").Guid;
        Assert.Contains($"{libBGuid}.Staging|Any CPU.ActiveCfg = Release|Any CPU", text);

        // every project has ActiveCfg + Build.0 for every solution config
        foreach (var project in GetProjectLines(text))
            foreach (var config in model.SolutionConfigurations)
            {
                Assert.Contains($"{project.Guid}.{config}.ActiveCfg = ", text);
                Assert.Contains($"{project.Guid}.{config}.Build.0 = ", text);
            }
    }

    [Fact]
    public void Merge_CreatesSolutionFolderPerSourceSolution_AndNestsProjects()
    {
        string merged = MergeFixtures();
        string text = File.ReadAllText(merged);

        Assert.Contains("Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"A\", \"A\"", text);
        Assert.Contains("Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"B\", \"B\"", text);
        Assert.Contains("GlobalSection(NestedProjects)", text);

        // all three projects are nested under a folder
        foreach (var project in GetProjectLines(text))
            Assert.Contains($"{project.Guid} = {{", text);
    }

    [Fact]
    public async Task Merge_MergedSolution_BuildsWithDotnet()
    {
        string merged = MergeFixtures();

        // nested dotnet build must not wait on the outer test run's MSBuild server (deadlocks for ~15min)
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1",
            ["MSBUILDDISABLENODEREUSE"] = "1"
        };
        var result = await ProcessRunner.RunAsync("dotnet", $"build \"{merged}\"", outputDir, environment: env);

        Assert.True(result.Success, "dotnet build failed:\n" + result.StdOut + result.StdErr);
    }
}
