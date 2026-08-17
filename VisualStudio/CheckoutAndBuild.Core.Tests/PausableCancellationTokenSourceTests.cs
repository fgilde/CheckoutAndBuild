using System;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild.Core.Pipeline;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
    public class PausableCancellationTokenSourceTests
    {
        [Fact]
        public async Task NotPaused_WaitReturnsImmediately()
        {
            using (var source = new PausableCancellationTokenSource())
            {
                var wait = source.WaitWhilePausedAsync();
                Assert.True(wait.IsCompleted || await Task.WhenAny(wait, Task.Delay(1000)) == wait);
            }
        }

        [Fact]
        public async Task Paused_WaitBlocksUntilResume()
        {
            using (var source = new PausableCancellationTokenSource())
            {
                source.Pause();
                var wait = source.WaitWhilePausedAsync();
                await Task.Delay(200);
                Assert.False(wait.IsCompleted);
                source.Resume();
                Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(10000)));
            }
        }

        [Fact]
        public async Task Cancel_WhilePaused_ThrowsInsteadOfHanging()
        {
            using (var source = new PausableCancellationTokenSource())
            {
                source.Pause();
                var wait = source.WaitWhilePausedAsync();
                source.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
            }
        }

        [Fact]
        public void LinkedToken_PropagatesCancellation()
        {
            using (var outer = new CancellationTokenSource())
            using (var source = new PausableCancellationTokenSource(outer.Token))
            {
                outer.Cancel();
                Assert.True(source.IsCancellationRequested);
            }
        }
    }
}
