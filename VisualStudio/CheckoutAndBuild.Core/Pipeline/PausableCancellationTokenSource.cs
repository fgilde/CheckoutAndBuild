using System;
using System.Threading;
using System.Threading.Tasks;

namespace CheckoutAndBuild.Core.Pipeline
{
    /// <summary>
    /// Cancellation source with an additional pause gate. Long-running operations call
    /// <see cref="WaitWhilePausedAsync"/> at safe points; Pause blocks them there until Resume.
    /// Replaces the old reflection-based PausableCancellationToken extensions.
    /// </summary>
    public sealed class PausableCancellationTokenSource : IDisposable
    {
        private readonly CancellationTokenSource cts;
        private volatile TaskCompletionSource<bool> pauseGate;

        public PausableCancellationTokenSource(CancellationToken linkedToken = default)
        {
            cts = linkedToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(linkedToken)
                : new CancellationTokenSource();
        }

        public CancellationToken Token => cts.Token;

        public bool IsPaused => pauseGate != null;

        public bool IsCancellationRequested => cts.IsCancellationRequested;

        public event EventHandler<bool> PausedChanged;

        public void Pause()
        {
            if (Interlocked.CompareExchange(ref pauseGate,
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), null) == null)
                PausedChanged?.Invoke(this, true);
        }

        public void Resume()
        {
            var gate = Interlocked.Exchange(ref pauseGate, null);
            if (gate != null)
            {
                gate.TrySetResult(true);
                PausedChanged?.Invoke(this, false);
            }
        }

        public void Cancel()
        {
            cts.Cancel();
            Resume();
        }

        /// <summary>Returns immediately when not paused; otherwise waits for Resume or cancellation.</summary>
        public async Task WaitWhilePausedAsync()
        {
            Token.ThrowIfCancellationRequested();
            var gate = pauseGate;
            if (gate == null)
                return;
            using (Token.Register(() => gate.TrySetCanceled(Token)))
                await gate.Task.ConfigureAwait(false);
            Token.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            Resume();
            cts.Dispose();
        }
    }
}
