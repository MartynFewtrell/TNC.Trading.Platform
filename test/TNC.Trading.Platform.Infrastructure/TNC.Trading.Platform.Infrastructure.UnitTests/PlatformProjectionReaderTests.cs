using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class PlatformProjectionReaderTests
{
    /// <summary>
    /// Traces to the Phase 6.4 strict query-purity contract.
    /// Verifies: the Infrastructure event projection adapter forwards filters and cancellation only to the read operation.
    /// Expected: event ordering and values are preserved, while the write operation remains unused.
    /// Why: the API event query must not enter reconciliation, notification, or persistence-write workflows.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ShouldForwardEventProjectionWithoutWrites_WhenFiltersAreProvided()
    {
        var expectedEvents = new[]
        {
            new OperationalEventModel(2, "auth", "Second", PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "summary-2", "details-2", DateTimeOffset.UtcNow),
            new OperationalEventModel(1, "auth", "First", PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "summary-1", "details-1", DateTimeOffset.UtcNow.AddMinutes(-1))
        };
        var store = new RecordingEventStore(expectedEvents);
        var reader = new EfPlatformEventsProjectionReader(store);

        var events = await reader.ReadAsync("auth", "Demo", CancellationToken.None);

        Assert.Equal(expectedEvents, events);
        Assert.Equal("auth", store.Category);
        Assert.Equal("Demo", store.Environment);
        Assert.Equal(1, store.ReadCount);
        Assert.Equal(0, store.WriteCount);
    }

    /// <summary>
    /// Traces to the Phase 6.4 cancellation contract.
    /// Verifies: cancellation is passed to the event projection store without being translated or followed by a write.
    /// Expected: the original cancellation exception propagates and the adapter performs one read and zero writes.
    /// Why: disconnected event requests must terminate without changing durable operational history.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ShouldPropagateCancellationWithoutWrites_WhenProjectionReadIsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var store = new RecordingEventStore(
            Array.Empty<OperationalEventModel>(),
            cancellationSource.Token);
        var reader = new EfPlatformEventsProjectionReader(store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(null, null, cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, store.CancellationToken);
        Assert.Equal(1, store.ReadCount);
        Assert.Equal(0, store.WriteCount);
    }

    private sealed class RecordingEventStore(
        IReadOnlyList<OperationalEventModel> events,
        CancellationToken cancellationTokenToThrow = default) : IPlatformEventStore
    {
        public string? Category { get; private set; }

        public string? Environment { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(
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

        public Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}