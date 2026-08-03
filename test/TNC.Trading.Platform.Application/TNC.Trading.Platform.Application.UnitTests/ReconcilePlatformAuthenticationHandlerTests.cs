using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class ReconcilePlatformAuthenticationHandlerTests
{
    /// Verifies the reconciliation command serializes concurrent writers so only one operation is active at a time.
    /// This protects persisted runtime state from overlapping startup, scheduled, manual, or configuration-triggered writes.
    [Fact]
    public async Task HandleAsync_ShouldSerializeReconciliation_WhenAnotherWriterIsActive()
    {
        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeWriters = 0;
        var maximumActiveWriters = 0;
        var invocationCount = 0;
        var reconciler = new DelegatingReconciler(async cancellationToken =>
        {
            var active = Interlocked.Increment(ref activeWriters);
            InterlockedMax(ref maximumActiveWriters, active);
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                firstEntered.SetResult(true);
            }
            else
            {
                secondEntered.SetResult(true);
            }

            await releaseFirst.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref activeWriters);
            return new PlatformRuntimeState { SessionStatus = PlatformSessionStatus.Active };
        });
        var handler = new ReconcilePlatformAuthenticationHandler(reconciler);

        var first = handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), CancellationToken.None);
        await firstEntered.Task;
        var second = handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), CancellationToken.None);
        Assert.False(secondEntered.Task.IsCompleted);
        releaseFirst.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumActiveWriters);
    }

    /// Verifies the command maps the persisted runtime state into its typed response after reconciliation completes.
    /// The response must expose the expected operator-safe outcome rather than provider or persistence representations.
    [Fact]
    public async Task HandleAsync_ShouldPersistExpectedOutcome_WhenAuthenticationCompletes()
    {
        var expected = new PlatformRuntimeState
        {
            SessionStatus = PlatformSessionStatus.Active,
            IsDegraded = false,
            LastValidatedAtUtc = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero)
        };
        var handler = new ReconcilePlatformAuthenticationHandler(new DelegatingReconciler(_ => Task.FromResult(expected)));

        var response = await handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Active, response.SessionStatus);
        Assert.False(response.IsDegraded);
        Assert.Null(response.FailureSummary);
        Assert.Equal(expected.LastValidatedAtUtc, response.ReconciledAtUtc);
    }

    /// Verifies cancellation passes through the command boundary without being translated into a business failure.
    /// Preserving the caller token allows shutdown and request cancellation to stop external work promptly.
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellation_WhenReconciliationIsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        var expected = new OperationCanceledException(cancellationSource.Token);
        var handler = new ReconcilePlatformAuthenticationHandler(
            new DelegatingReconciler(_ => Task.FromException<PlatformRuntimeState>(expected)));

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, actual.CancellationToken);
    }

    /// Verifies an expected provider failure is represented by the persisted degraded outcome exposed by the typed response.
    /// This keeps provider-specific exceptions out of the inward command contract while preserving actionable failure state.
    [Fact]
    public async Task HandleAsync_ShouldReturnTypedFailure_WhenProviderFails()
    {
        var expected = new PlatformRuntimeState
        {
            SessionStatus = PlatformSessionStatus.Degraded,
            IsDegraded = true,
            LatestFailureSummary = "IG authentication failed: broker is unreachable."
        };
        var handler = new ReconcilePlatformAuthenticationHandler(new DelegatingReconciler(_ => Task.FromResult(expected)));

        var response = await handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), CancellationToken.None);

        Assert.Equal(PlatformSessionStatus.Degraded, response.SessionStatus);
        Assert.True(response.IsDegraded);
        Assert.Equal(expected.LatestFailureSummary, response.FailureSummary);
    }

    private static void InterlockedMax(ref int location, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref location);
            if (value <= current || Interlocked.CompareExchange(ref location, value, current) == current)
            {
                return;
            }
        }
    }

    private sealed class DelegatingReconciler(
        Func<CancellationToken, Task<PlatformRuntimeState>> reconcile) : IPlatformAuthenticationReconciler
    {
        public Task<PlatformRuntimeState> ReconcileAsync(CancellationToken cancellationToken) => reconcile(cancellationToken);
    }
}