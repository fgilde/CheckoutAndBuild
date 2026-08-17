using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CheckoutAndBuild.Core.Execution
{
    public sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string stdOut, string stdErr)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public int ExitCode { get; }
        public string StdOut { get; }
        public string StdErr { get; }
        public bool Success => ExitCode == 0;
    }

    public static class ProcessRunner
    {
        public static async Task<ProcessResult> RunAsync(
            string fileName, string arguments, string workingDirectory = null,
            Action<string> onOutputLine = null, Action<string> onErrorLine = null,
            IDictionary<string, string> environment = null,
            CancellationToken cancellationToken = default,
            ProcessPriorityClass? priority = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (!string.IsNullOrEmpty(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;
            if (environment != null)
            {
                foreach (var pair in environment)
                    startInfo.EnvironmentVariables[pair.Key] = pair.Value;
            }

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var outputDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var errorDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null) { outputDone.TrySetResult(true); return; }
                    lock (stdOut) stdOut.AppendLine(e.Data);
                    onOutputLine?.Invoke(e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null) { errorDone.TrySetResult(true); return; }
                    lock (stdErr) stdErr.AppendLine(e.Data);
                    onErrorLine?.Invoke(e.Data);
                };
                process.Exited += (s, e) => exited.TrySetResult(true);

                process.Start();
                if (priority.HasValue)
                {
                    try { process.PriorityClass = priority.Value; }
                    catch (Exception) { /* exited early or no rights — priority is best effort */ }
                }
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() => KillProcessTree(process)))
                {
                    await Task.WhenAll(exited.Task, outputDone.Task, errorDone.Task).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException($"Process '{fileName}' was canceled and killed.");

                return new ProcessResult(process.ExitCode, stdOut.ToString(), stdErr.ToString());
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (process.HasExited)
                    return;

                using (var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/T /F /PID " + process.Id,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    killer?.WaitForExit(5000);
                }
            }
            catch
            {
            }
        }
    }
}
