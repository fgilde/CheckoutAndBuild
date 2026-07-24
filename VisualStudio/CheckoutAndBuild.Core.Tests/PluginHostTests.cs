using System.ComponentModel.Composition;
using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Model;
using CheckoutAndBuild.Core.Plugins;
using CheckoutAndBuild.Core.Services;

namespace CheckoutAndBuild.Core.Tests;

/// <summary>Discovered via [InheritedExport] on ICheckoutAndBuildPlugin.</summary>
public class TestPlugin : ICheckoutAndBuildPlugin
{
    public bool InitCalled { get; private set; }
    public IServiceProvider? ServiceProvider { get; private set; }

    public Task Init(IServiceProvider serviceProvider, string pluginDirectory)
    {
        InitCalled = true;
        ServiceProvider = serviceProvider;
        return Task.CompletedTask;
    }
}

[Export(typeof(ICustomAction))]
public class TestCustomAction : ICustomAction
{
    public void RunPreAction(IOperationService service, ISolutionProjectModel solutionFile, IServiceSettings settings) { }
    public void RunPostAction(IOperationService service, ISolutionProjectModel solutionFile, object result, IServiceSettings settings) { }
}

public class PluginHostTests
{
    [Fact]
    public async Task Load_FindsPluginAndCustomAction_AndCallsInit()
    {
        var host = new PluginHost();
        var provider = new StubServiceProvider();

        var errors = await host.LoadAsync(Array.Empty<string>(), provider, typeof(PluginHostTests).Assembly);

        var plugin = Assert.Single(host.GetExportedValues<ICheckoutAndBuildPlugin>().OfType<TestPlugin>());
        Assert.True(plugin.InitCalled);
        Assert.Same(provider, plugin.ServiceProvider);
        Assert.Single(host.GetExportedValues<ICustomAction>().OfType<TestCustomAction>());
        Assert.Empty(errors);
        Assert.True(host.IsLoaded);
    }

    [Fact]
    public async Task Load_BrokenDll_IsToleratedAndReported()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "broken.dll"), "this is not a dll");
        try
        {
            var host = new PluginHost();
            var errors = await host.LoadAsync(new[] { dir }, null, typeof(PluginHostTests).Assembly);

            Assert.Contains(errors, e => e.Contains("broken.dll"));
            // load stays functional despite the broken DLL
            Assert.NotEmpty(host.GetExportedValues<ICheckoutAndBuildPlugin>());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetExportedValues_BeforeLoad_ReturnsEmpty()
    {
        Assert.Empty(new PluginHost().GetExportedValues<ICustomAction>());
    }

    [Fact]
    public void BuildService_MergesPluginBuildProperties_IntoCommand()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestSolution.sln");
        var model = SolutionParser.Parse(fixture);
        var service = new BuildService
        {
            BuildPropertiesProviders = new IProjectBuildPropertiesProvider[] { new FakePropertiesProvider() }
        };

        string script = service.GetScript(new[] { model }, null, ScriptExportType.Batch);

        Assert.Contains("/p:Foo=\"Bar\"", script);
    }

    private sealed class FakePropertiesProvider : IProjectBuildPropertiesProvider
    {
        public IDictionary<string, string> GetDefaultBuildProperties(ISolutionProjectModel project, IServiceSettings settings)
            => new Dictionary<string, string> { ["Foo"] = "Bar" };
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
