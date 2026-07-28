using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Api.Hosting;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class PlatformAuthenticationSupervisorTests
{
    /// <summary>
    /// Trace: Clean Architecture Migration Phase 2, Step 2.2.
    /// Verifies: API-owned supervision continues scheduling after one transient reconciliation failure.
    /// Expected: the failed tick is logged and the next tick runs before the injected delay cancels the loop.
    /// Why: a temporary provider or persistence failure must not permanently stop background recovery.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldContinueAfterTransientFailure_WhenNextTickRuns()
    {
        using var cancellationSource = new CancellationTokenSource();
        var tickRunner = new FakeTickRunner([new InvalidOperationException("transient"), null, null]);
        var delay = new CancellingDelay(cancellationSource, cancelOnCall: 2);
        var supervisor = new PlatformAuthenticationSupervisor(
            tickRunner,
            NullLogger<PlatformAuthenticationSupervisor>.Instance,
            delay);

        await supervisor.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Equal(3, tickRunner.CallCount);
        Assert.Equal(2, delay.CallCount);
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 2, Step 2.2.
    /// Verifies: host cancellation stops the API-owned supervision loop without waiting for another cadence.
    /// Expected: a cancellation raised by the tick is handled at the loop boundary and returns promptly.
    /// Why: shutdown must not be delayed by reconciliation or scheduling work.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldStopPromptly_WhenCancellationIsRequested()
    {
        using var cancellationSource = new CancellationTokenSource();
        var tickRunner = new CancellingTickRunner(cancellationSource);
        var supervisor = new PlatformAuthenticationSupervisor(
            tickRunner,
            NullLogger<PlatformAuthenticationSupervisor>.Instance,
            new NeverReachedDelay());

        await supervisor.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Equal(1, tickRunner.CallCount);
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 2, Step 2.2.
    /// Verifies: the hosted adapter completes its initial reconciliation before the host proceeds to normal service startup.
    /// Expected: StartAsync invokes exactly one initial tick before its hosted execute loop begins.
    /// Why: readiness must not report healthy before the initial platform state is established.
    /// </summary>
    [Fact]
    public async Task StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy()
    {
        using var cancellationSource = new CancellationTokenSource();
        var tickRunner = new FakeTickRunner([null, null]);
        var supervisor = new PlatformAuthenticationSupervisor(
            tickRunner,
            NullLogger<PlatformAuthenticationSupervisor>.Instance,
            new CancellingDelay(cancellationSource, cancelOnCall: 1));

        await supervisor.StartAsync(cancellationSource.Token);
        await supervisor.StopAsync(cancellationSource.Token);

        Assert.Equal(1, tickRunner.CallCount);
    }

    private sealed class FakeTickRunner(IEnumerable<Exception?> failures) : IPlatformAuthenticationSupervisorTickRunner
    {
        private readonly Queue<Exception?> remainingFailures = new(failures);

        public int CallCount { get; private set; }

        public Task RunSingleTickAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            var failure = remainingFailures.Dequeue();
            return failure is null
                ? Task.CompletedTask
                : Task.FromException(failure);
        }
    }

    private sealed class CancellingTickRunner(CancellationTokenSource cancellationSource) : IPlatformAuthenticationSupervisorTickRunner
    {
        public int CallCount { get; private set; }

        public Task RunSingleTickAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationSource.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed class CancellingDelay(CancellationTokenSource cancellationSource, int cancelOnCall) : IPlatformAuthenticationSupervisorDelay
    {
        public int CallCount { get; private set; }

        public Task DelayAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount == cancelOnCall)
            {
                cancellationSource.Cancel();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class NeverReachedDelay : IPlatformAuthenticationSupervisorDelay
    {
        public Task DelayAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}