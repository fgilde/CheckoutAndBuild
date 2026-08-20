using CheckoutAndBuild.Core.Model;

namespace CheckoutAndBuild.Core.Tests;

public class SolutionParserTests
{
    private static readonly string SlnPath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");

    private static SolutionProjectModel Parse() => SolutionParser.Parse(SlnPath);

    [Fact]
    public void Parse_FindsTwoProjects_SkipsSolutionFolder()
    {
        var model = Parse();
        Assert.Equal(2, model.GetSolutionProjects().Count);
        Assert.Equal(new[] { "ClassicLib", "SdkTests" }, model.Projects.Select(p => p.Name).OrderBy(n => n));
        Assert.DoesNotContain(model.Projects, p => p.Name == "Solution Items");
    }

    [Fact]
    public void Parse_ProjectPaths_AreAbsoluteAndExist()
    {
        var model = Parse();
        Assert.All(model.GetSolutionProjects(), p =>
        {
            Assert.True(Path.IsPathRooted(p));
            Assert.True(File.Exists(p), $"missing: {p}");
        });
    }

    [Fact]
    public void Parse_ClassicProject_ReadsExplicitDebugOutputPath()
    {
        var classic = Parse().Projects.Single(p => p.Name == "ClassicLib");
        Assert.EndsWith(Path.Combine("ClassicLib", "bin", "DebugOut"), classic.OutputPath.TrimEnd('\\', '/'));
        Assert.EndsWith(Path.Combine("ClassicLib", "obj", "DebugOut"), classic.IntermediateOutputPath.TrimEnd('\\', '/'));
        Assert.Equal("ClassicLib.Assembly", classic.AssemblyName);
        Assert.Equal("v4.8", classic.TargetFramework);
        Assert.False(classic.IsSdkStyle);
    }

    [Fact]
    public void Parse_SdkProject_UsesDefaultOutputPath()
    {
        var sdk = Parse().Projects.Single(p => p.Name == "SdkTests");
        Assert.EndsWith(Path.Combine("SdkTests", "bin", "Debug", "net8.0"), sdk.OutputPath.TrimEnd('\\', '/'));
        Assert.EndsWith(Path.Combine("SdkTests", "obj", "Debug", "net8.0"), sdk.IntermediateOutputPath.TrimEnd('\\', '/'));
        Assert.Equal("SdkTests", sdk.AssemblyName);
        Assert.Equal("net8.0", sdk.TargetFramework);
        Assert.True(sdk.IsSdkStyle);
    }

    [Fact]
    public void Parse_DetectsXunitProjectAsUnitTestProject()
    {
        var model = Parse();
        var testProject = Assert.Single(model.GetUnitTestProjects());
        Assert.EndsWith("SdkTests.csproj", testProject);
    }

    [Fact]
    public void Parse_ReadsSolutionConfigurations()
    {
        var model = Parse();
        Assert.Contains("Debug|Any CPU", model.SolutionConfigurations);
        Assert.Contains("Release|Any CPU", model.SolutionConfigurations);
    }

    [Fact]
    public void Parse_Slnx_FindsProjectsInFoldersAndReadsBuildTypes()
    {
        string dir = Path.Combine(Path.GetTempPath(), "coab-slnx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Lib"));
        File.WriteAllText(Path.Combine(dir, "Lib", "Lib.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        string slnx = Path.Combine(dir, "Test.slnx");
        File.WriteAllText(slnx,
            "<Solution>" +
            "<Configurations><BuildType Name=\"Debug\" /><BuildType Name=\"Staging\" /></Configurations>" +
            "<Folder Name=\"/src/\"><Project Path=\"Lib/Lib.csproj\" /></Folder>" +
            "<Project Path=\"Missing/Missing.csproj\" />" +
            "<Project Path=\"Docs/readme.md\" />" +
            "</Solution>");
        try
        {
            var model = SolutionParser.Parse(slnx);
            Assert.Equal(2, model.Projects.Count);
            var lib = model.Projects.Single(p => p.Name == "Lib");
            Assert.True(lib.IsSdkStyle);
            Assert.Equal("net8.0", lib.TargetFramework);
            Assert.True(Path.IsPathRooted(lib.ProjectFilePath));
            Assert.All(model.Projects, p => Assert.Matches(@"^\{[0-9A-F\-]+\}$", p.ProjectGuid));
            Assert.Contains("Debug|Any CPU", model.SolutionConfigurations);
            Assert.Contains("Staging|Any CPU", model.SolutionConfigurations);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Model_Defaults_AreSensible()
    {
        var model = Parse();
        Assert.Equal("TestSolution.sln", model.SolutionFileName);
        Assert.Equal(Path.GetDirectoryName(SlnPath), model.SolutionFolder);
        Assert.Equal(SlnPath, model.ItemPath);
        Assert.True(model.IsIncluded);
        Assert.Equal(0, model.BuildPriority);
        Assert.False(model.IsBusy);
        Assert.False(model.IsDelphiProject);
        Assert.Null(model.ErrorContent);
        Assert.NotNull(model.BuildTargets);
        Assert.NotNull(model.BuildProperties);
    }

    [Fact]
    public void SetBuildTargets_TrimsEntries_EmptyFallsBackToBuild()
    {
        var model = new SolutionProjectModel("x.sln");
        model.SetBuildTargets(new[] { " Clean ", "Build", "  ", null });
        Assert.Equal(new[] { "Clean", "Build" }, model.BuildTargets);
        model.SetBuildTargets(null);
        Assert.Equal(new[] { "Build" }, model.BuildTargets);
    }

    [Fact]
    public void Model_Progress_DrivesCurrentOperation()
    {
        var model = Parse();
        model.CurrentOperation = new CheckoutAndBuild.Core.Contracts.OperationInfo(4);
        Assert.True(model.IsBusy);
        model.ResetProgress();
        Assert.Equal(0, model.CurrentOperation.Progress);
        model.IncrementProgress();
        Assert.Equal(50, model.CurrentOperation.Progress);
    }
}
