using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents.Ports;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus.Ports;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class PlatformProjectionQueryTests
{
    /// <summary>
    /// Traces to the Phase 2.3 query-purity requirement.
    /// Verifies: the status handler delegates only to its projection reader.
    /// Expected: the projection reader is called once and no reconciliation seam is available to the handler.
    /// Why: a status refresh must not authenticate, persist, notify, or transition runtime state.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldNotInvokeReconciliation_WhenStatusIsRead()
    {
        var projectionReader = new DelegatingStatusProjectionReader(
            new PlatformStatusProjection(null, null));
        var handler = new GetPlatformStatusHandler(projectionReader);

        _ = await handler.HandleAsync(new GetPlatformStatusRequest(), CancellationToken.None);

        Assert.Equal(1, projectionReader.ReadCount);
    }

    /// <summary>
    /// Traces to the Phase 2.3 missing-state contract.
    /// Verifies: an absent persisted runtime projection is returned as an explicit null status.
    /// Expected: the response preserves null status and null freshness metadata.
    /// Why: querying a newly provisioned database must not create runtime state as a side effect.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnExplicitMissingState_WhenRuntimeStateDoesNotExist()
    {
        var handler = new GetPlatformStatusHandler(
            new DelegatingStatusProjectionReader(new PlatformStatusProjection(null, null)));

        var response = await handler.HandleAsync(new GetPlatformStatusRequest(), CancellationToken.None);

        Assert.Null(response.Status);
        Assert.Null(response.LastReconciledAtUtc);
    }

    /// <summary>
    /// Traces to the Phase 2.3 freshness contract.
    /// Verifies: the status handler returns the persisted last-validation timestamp supplied by the projection.
    /// Expected: freshness metadata is preserved without calculating or updating it during the read.
    /// Why: consumers need to distinguish an available but stale projection from a newly reconciled one.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnFreshnessMetadata_WhenLatestStateExists()
    {
        var reconciledAt = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
        var handler = new GetPlatformStatusHandler(
            new DelegatingStatusProjectionReader(new PlatformStatusProjection(null, reconciledAt)));

        var response = await handler.HandleAsync(new GetPlatformStatusRequest(), CancellationToken.None);

        Assert.Equal(reconciledAt, response.LastReconciledAtUtc);
    }

    /// <summary>
    /// Traces to the Phase 2.3 event-query purity requirement.
    /// Verifies: event requests delegate directly to the feature-local projection reader with their filters.
    /// Expected: the returned event sequence and filter values are unchanged.
    /// Why: event browsing must read history without invoking reconciliation or another writer workflow.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldOnlyReadEvents_WhenEventsAreRequested()
    {
        var expectedEvents = new[] { CreateEvent(2), CreateEvent(1) };
        var projectionReader = new DelegatingEventsProjectionReader(expectedEvents);
        var handler = new GetPlatformEventsHandler(projectionReader);

        var response = await handler.HandleAsync(
            new GetPlatformEventsRequest("auth", "Demo"),
            CancellationToken.None);

        Assert.Equal(expectedEvents, response.Events);
        Assert.Equal("auth", projectionReader.Category);
        Assert.Equal("Demo", projectionReader.Environment);
    }

    /// <summary>
    /// Traces to the Phase 2.3 no-write requirement.
    /// Verifies: event reads have no write collaborator and invoke only the read method once.
    /// Expected: the projection reader records one read and zero writes.
    /// Why: reading operational history must never insert, save, notify, or change runtime state.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPerformNoWrites_WhenEventsAreRequested()
    {
        var projectionReader = new DelegatingEventsProjectionReader(Array.Empty<OperationalEventModel>());
        var handler = new GetPlatformEventsHandler(projectionReader);

        _ = await handler.HandleAsync(new GetPlatformEventsRequest(null, null), CancellationToken.None);

        Assert.Equal(1, projectionReader.ReadCount);
        Assert.Equal(0, projectionReader.WriteCount);
    }

    /// <summary>
    /// Traces to the Phase 6.4 cancellation contract.
    /// Verifies: status-query cancellation crosses the feature boundary unchanged.
    /// Expected: the reader receives the caller token and its cancellation exception reaches the caller.
    /// Why: a read must stop promptly without falling back to reconciliation or another workflow.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellation_WhenStatusProjectionReadIsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var projectionReader = new DelegatingStatusProjectionReader(
            new PlatformStatusProjection(null, null),
            cancellationSource.Token);
        var handler = new GetPlatformStatusHandler(projectionReader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new GetPlatformStatusRequest(), cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, projectionReader.CancellationToken);
    }

    /// <summary>
    /// Traces to the Phase 6.4 cancellation contract.
    /// Verifies: event-query cancellation crosses the feature boundary unchanged.
    /// Expected: the reader receives the caller token and no write collaborator is invoked.
    /// Why: event browsing must remain a bounded read even when the caller disconnects.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellationWithoutWrites_WhenEventProjectionReadIsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var projectionReader = new DelegatingEventsProjectionReader(
            Array.Empty<OperationalEventModel>(),
            cancellationSource.Token);
        var handler = new GetPlatformEventsHandler(projectionReader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new GetPlatformEventsRequest(null, null), cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, projectionReader.CancellationToken);
        Assert.Equal(0, projectionReader.WriteCount);
    }

    private static OperationalEventModel CreateEvent(long eventId)
        => new(eventId, "auth", $"Event{eventId}", PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "summary", "details", DateTimeOffset.UtcNow);

    private sealed class DelegatingStatusProjectionReader : IPlatformStatusProjectionReader
    {
        private readonly PlatformStatusProjection projection;
        private readonly CancellationToken cancellationTokenToThrow;

        public DelegatingStatusProjectionReader(
            PlatformStatusProjection projection,
            CancellationToken cancellationTokenToThrow = default)
        {
            this.projection = projection;
            this.cancellationTokenToThrow = cancellationTokenToThrow;
        }

        public int ReadCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<PlatformStatusProjection> ReadAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            CancellationToken = cancellationToken;
            if (cancellationTokenToThrow.CanBeCanceled)
            {
                return Task.FromCanceled<PlatformStatusProjection>(cancellationTokenToThrow);
            }

            return Task.FromResult(projection);
        }
    }

    private sealed class DelegatingEventsProjectionReader(
        IReadOnlyList<OperationalEventModel> events,
        CancellationToken cancellationTokenToThrow = default) : IPlatformEventsProjectionReader
    {
        private readonly CancellationToken cancellationTokenToThrow = cancellationTokenToThrow;

        public string? Category { get; private set; }

        public string? Environment { get; private set; }

        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<OperationalEventModel>> ReadAsync(
            string? category,
            string? environment,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            Category = category;
            Environment = environment;
            CancellationToken = cancellationToken;
            if (cancellationTokenToThrow.CanBeCanceled)
            {
                return Task.FromCanceled<IReadOnlyList<OperationalEventModel>>(cancellationTokenToThrow);
            }

            return Task.FromResult(events);
        }
    }
}