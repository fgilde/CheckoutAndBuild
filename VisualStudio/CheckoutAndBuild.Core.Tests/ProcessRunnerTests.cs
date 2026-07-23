using System.Diagnostics;
using CheckoutAndBuild.Core.Execution;

namespace CheckoutAndBuild.Core.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task EchoHi_ExitCode0_StdOutContainsHi()
    {
        var result = await ProcessRunner.RunAsync("cmd", "/c echo hi");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Success);
        Assert.Contains("hi", result.StdOut);
    }

    [Fact]
    public async Task Exit3_ExitCode3_SuccessFalse()
    {
        var result = await ProcessRunner.RunAsync("cmd", "/c exit 3");
        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task OnOutputLine_CalledPerLine()
    {
        var lines = new List<string>();
        var result = await ProcessRunner.RunAsync("cmd", "/c \"echo a& echo b\"",
            onOutputLine: lines.Add);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(lines, l => l.Trim() == "a");
        Assert.Contains(lines, l => l.Trim() == "b");
    }

    [Fact]
    public async Task Cancellation_KillsProcess_ThrowsCanceled()
    {
        using var cts = new CancellationTokenSource(500);
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessRunner.RunAsync("cmd", "/c \"ping -n 30 127.0.0.1 >nul\"",
                cancellationToken: cts.Token));
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Cancellation took {sw.Elapsed}");
    }
}
