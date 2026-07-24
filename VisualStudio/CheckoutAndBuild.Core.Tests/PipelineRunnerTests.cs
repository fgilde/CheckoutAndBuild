using CheckoutAndBuild.Core.Contracts;
using CheckoutAndBuild.Core.Contracts.Service;
using CheckoutAndBuild.Core.Contracts.Settings;
using CheckoutAndBuild.Core.Pipeline;

namespace CheckoutAndBuild.Core.Tests;

public class PipelineRunnerTests
{
    private static readonly PipelineRunner Runner = new();

    [Fact]
    public async Task Services_RunSequentially_SortedByOrder()
    {
        var log = new List<string>();
        var services = new[]
        {
            new FakeService("third", 30, log),
            new FakeService("first", 10, log),
            new FakeService("second", 20, log)
        };

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new[] { Project(1) }, services, new PipelineContext(), pcs);

        Assert.Equal(new[] { "first", "second", "third" }, log);
    }

    [Fact]
    public async Task ExcludedProjects_AreFiltered_AndIncludedSortedByBuildPriority()
    {
        var log = new List<string>();
        var service = new FakeService("svc", 1, log);
        var excluded = Project(0);
        excluded.IsIncluded = false;
        var high = Project(5);
        var low = Project(1);

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new ISolutionProjectModel[] { high, excluded, low }, new[] { service }, new PipelineContext(), pcs);

        Assert.Equal(new ISolutionProjectModel[] { low, high }, service.ReceivedProjects);
    }

    [Fact]
    public async Task Cancel_DuringService_StopsPipeline_WithOperationCanceled()
    {
        var log = new List<string>();
        using var pcs = new PausableCancellationTokenSource();
        var first = new FakeService("first", 1, log) { OnExecute = _ => { pcs.Cancel(); return Task.CompletedTask; } };
        var second = new FakeService("second", 2, log);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runner.RunAsync(new[] { Project(1) }, new[] { first, second }, new PipelineContext(), pcs));

        Assert.Equal(new[] { "first" }, log);
    }

    [Fact]
    public async Task Pause_BlocksProgress_UntilResume()
    {
        var log = new List<string>();
        var service = new FakeService("svc", 1, log);
        using var pcs = new PausableCancellationTokenSource();
        pcs.Pause();

        var run = Runner.RunAsync(new[] { Project(1) }, new[] { service }, new PipelineContext(), pcs);
        await Task.Delay(300);
        Assert.Empty(log);
        Assert.False(run.IsCompleted);

        pcs.Resume();
        await run;
        Assert.Equal(new[] { "svc" }, log);
    }

    [Fact]
    public async Task PreBuildScript_NonZeroExit_ThrowsInvalidOperation_NoServiceRuns()
    {
        var log = new List<string>();
        var service = new FakeService("svc", 1, log);
        var script = WriteTempCmd("exit /b 7");
        using var pcs = new PausableCancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner.RunAsync(new[] { Project(1) }, new[] { service }, new PipelineContext { PreBuildScript = script }, pcs));

        Assert.Empty(log);
    }

    [Fact]
    public async Task PostBuildScript_RunsAfterBuildService_BeforeNextService()
    {
        var log = new List<string>();
        var marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".marker");
        var script = WriteTempCmd($"echo done> \"{marker}\"");

        bool markerExistedAtNextService = false;
        var build = new FakeService("build", 1, log, new Guid(ServiceIds.BuildServiceId));
        var next = new FakeService("next", 2, log) { OnExecute = _ => { markerExistedAtNextService = File.Exists(marker); return Task.CompletedTask; } };

        using var pcs = new PausableCancellationTokenSource();
        try
        {
            await Runner.RunAsync(new[] { Project(1) }, new[] { build, next },
                new PipelineContext { PostBuildScript = script }, pcs);

            Assert.True(File.Exists(marker));
            Assert.True(markerExistedAtNextService, "post-build script must complete before the next service runs");
            Assert.Equal(new[] { "build", "next" }, log);
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [Fact]
    public async Task PostBuildScript_Failure_ReportedToProgress_NotThrown()
    {
        var log = new List<string>();
        var reports = new List<PipelineProgress>();
        var build = new FakeService("build", 1, log, new Guid(ServiceIds.BuildServiceId));
        var script = WriteTempCmd("exit /b 3");

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new[] { Project(1) }, new[] { build },
            new PipelineContext { PostBuildScript = script, Progress = new CollectingProgress(reports) }, pcs);

        Assert.Contains(reports, r => r.Error != null && r.Error.Contains("3"));
    }

    [Fact]
    public async Task Progress_ReportedBeforeEachService()
    {
        var log = new List<string>();
        var reports = new List<PipelineProgress>();
        var services = new[] { new FakeService("a", 1, log), new FakeService("b", 2, log) };

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new[] { Project(1) }, services,
            new PipelineContext { Progress = new CollectingProgress(reports) }, pcs);

        Assert.Equal(new[] { "a", "b" }, reports.Where(r => r.Error == null).Select(r => r.OperationName));
        Assert.All(reports, r => Assert.Equal(2, r.ServiceCount));
        Assert.Equal(new[] { 0, 1 }, reports.Where(r => r.Error == null).Select(r => r.ServiceIndex));
    }

    [Fact]
    public async Task ServiceProjectFilter_RoutesProjectsPerService_AndSkipsEmptyServices()
    {
        var log = new List<string>();
        var svcA = new FakeService("a", 1, log);
        var svcB = new FakeService("b", 2, log);
        var svcC = new FakeService("c", 3, log);
        var p1 = Project(1);
        var p2 = Project(2);

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new ISolutionProjectModel[] { p1, p2 }, new[] { svcA, svcB, svcC },
            new PipelineContext
            {
                ServiceProjectFilter = (svc, model) => svc == svcA ? model == p1 : svc != svcC
            }, pcs);

        Assert.Equal(new ISolutionProjectModel[] { p1 }, svcA.ReceivedProjects);
        Assert.Equal(new ISolutionProjectModel[] { p1, p2 }, svcB.ReceivedProjects);
        Assert.Equal(new[] { "a", "b" }, log); // svcC had no enabled projects and was skipped entirely
    }

    [Fact]
    public async Task ServiceProjectFilter_EmptyService_SkipsItsCustomActions()
    {
        var log = new List<string>();
        var svc = new FakeService("svc", 1, log);

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new[] { Project(1) }, new[] { svc },
            new PipelineContext
            {
                ServiceProjectFilter = (s, m) => false,
                CustomActions = new[] { new RecordingAction(log) }
            }, pcs);

        Assert.Empty(log);
    }

    [Fact]
    public async Task CustomActions_RunBeforeAndAfterEachService_InOrder()
    {
        var log = new List<string>();
        var service = new FakeService("svc", 1, log);

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new[] { Project(1) }, new[] { service },
            new PipelineContext { CustomActions = new[] { new RecordingAction(log) } }, pcs);

        Assert.Equal(new[] { "pre:svc", "svc", "post:svc" }, log);
    }

    [Fact]
    public async Task CustomAction_Failure_ReportedToProgress_ServiceStillRuns()
    {
        var log = new List<string>();
        var reports = new List<PipelineProgress>();
        var service = new FakeService("svc", 1, log);

        using var pcs = new PausableCancellationTokenSource();
        await Runner.RunAsync(new[] { Project(1) }, new[] { service },
            new PipelineContext
            {
                CustomActions = new ICustomAction[] { new ThrowingAction() },
                Progress = new CollectingProgress(reports)
            }, pcs);

        Assert.Equal(new[] { "svc" }, log);
        Assert.Equal(2, reports.Count(r => r.Error != null && r.Error.Contains("ThrowingAction"))); // pre + post
    }

    private static string WriteTempCmd(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cmd");
        File.WriteAllText(path, "@echo off\r\n" + body + "\r\n");
        return path;
    }

    private static FakeProject Project(int priority) => new() { BuildPriority = priority };

    /// <summary>IProgress&lt;T&gt; that reports synchronously (Progress&lt;T&gt; posts to a sync context and races the assertions).</summary>
    private sealed class CollectingProgress : IProgress<PipelineProgress>
    {
        private readonly List<PipelineProgress> reports;
        public CollectingProgress(List<PipelineProgress> reports) => this.reports = reports;
        public void Report(PipelineProgress value) { lock (reports) reports.Add(value); }
    }

    private sealed class RecordingAction : ICustomAction
    {
        private readonly List<string> log;
        public RecordingAction(List<string> log) => this.log = log;
        public void RunPreAction(IOperationService service, ISolutionProjectModel solutionFile, IServiceSettings settings) => log.Add($"pre:{service.OperationName}");
        public void RunPostAction(IOperationService service, ISolutionProjectModel solutionFile, object result, IServiceSettings settings) => log.Add($"post:{service.OperationName}");
    }

    private sealed class ThrowingAction : ICustomAction
    {
        public void RunPreAction(IOperationService service, ISolutionProjectModel solutionFile, IServiceSettings settings) => throw new InvalidOperationException("boom");
        public void RunPostAction(IOperationService service, ISolutionProjectModel solutionFile, object result, IServiceSettings settings) => throw new InvalidOperationException("boom");
    }

    private sealed class FakeService : IOperationService
    {
        private readonly List<string> log;

        public FakeService(string name, int order, List<string> log, Guid? serviceId = null)
        {
            OperationName = name;
            Order = order;
            this.log = log;
            ServiceId = serviceId ?? Guid.NewGuid();
        }

        public Func<IEnumerable<ISolutionProjectModel>, Task>? OnExecute { get; set; }
        public IReadOnlyList<ISolutionProjectModel>? ReceivedProjects { get; private set; }

        public int Order { get; }
        public Guid ServiceId { get; }
        public string OperationName { get; }
        public bool AllowScriptExport => false;
        public ScriptExportType[] SupportedScriptExportTypes => Array.Empty<ScriptExportType>();

        public async Task ExecuteAsync(IEnumerable<ISolutionProjectModel> solutionProjects, IServiceSettings settings, PausableCancellationTokenSource cancellation)
        {
            ReceivedProjects = solutionProjects.ToList();
            log.Add(OperationName);
            if (OnExecute != null)
                await OnExecute(solutionProjects);
        }

        public string GetScript(IEnumerable<ISolutionProjectModel> models, IServiceSettings settings, ScriptExportType scriptExportType) => string.Empty;
        public void Cancel() { }
        public void Cancel(ISolutionProjectModel solution) { }
        public bool IsCancelled(ISolutionProjectModel solution) => false;
    }

    private sealed class FakeProject : ISolutionProjectModel
    {
        public OperationInfo CurrentOperation { get; set; } = null!;
        public string ItemPath => "fake.sln";
        public bool IsIncluded { get; set; } = true;
        public int BuildPriority { get; set; }
        public string SolutionFileName => ItemPath;
        public bool IsGitSourceControlled => false;
        public string SolutionFolder => ".";
        public bool IsDelphiProject => false;
        public object ErrorContent { get; set; } = null!;
        public bool IsBusy => false;
        public IReadOnlyCollection<string> GetUnitTestProjects() => Array.Empty<string>();
        public IReadOnlyCollection<string> GetSolutionProjects() => Array.Empty<string>();
        public IEnumerable<string> BuildTargets => Array.Empty<string>();
        public IDictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();
        public void SetResult(object result) { }
        public void ResetProgress() { }
        public void IncrementProgress() { }
    }
}
