using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketCategoryInstruments;

public sealed class GetMarketCategoryInstrumentPageHandlerTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 1.
    /// Verifies: the first page uses the current saved snapshot and distinguishes a category that has never been collected.
    /// Expected: the handler reports NeverCollected while reading only the category and snapshot ports.
    /// Why: browsing must not trigger provider collection and must not confuse missing history with an empty complete snapshot.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnNeverCollected_WhenCurrentCategoryHasNoSnapshot()
    {
        var reader = new SnapshotReader();
        var handler = CreateHandler(reader);

        var response = await handler.HandleAsync(new("FX", 50, null, null, null, null), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentPageReadStatus.NeverCollected, response.Status);
        Assert.Equal(1, reader.ReadCount);
        Assert.Null(reader.LastRequest?.SnapshotVersion);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 1.
    /// Verifies: a cursor bound to another category is rejected before the SQL snapshot reader is called.
    /// Expected: the handler reports StaleCursor and performs no page read.
    /// Why: opaque cursors must remain bound to their original category and must not be reusable across API resources.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectCursorBoundToAnotherCategory_BeforeReadingSnapshot()
    {
        var reader = new SnapshotReader();
        var handler = CreateHandler(reader);

        var response = await handler.HandleAsync(new("FX", 50, 4, "Equities", "EPIC", "Demo"), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentPageReadStatus.StaleCursor, response.Status);
        Assert.Equal(0, reader.ReadCount);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 1.
    /// Verifies: a saved category can have a complete empty snapshot and a cursor cannot continue after its version is gone.
    /// Expected: a first page returns Page even when its instrument list is empty; a missing versioned page returns StaleCursor.
    /// Why: complete-empty is meaningful collection evidence, whereas a disappeared version must require restarting pagination.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldDistinguishCompleteEmptyFromStaleVersion_WhenReadingPages()
    {
        var emptyPage = new MarketCategoryInstrumentSnapshotPage(5, DateTimeOffset.UtcNow, [], null);
        var reader = new SnapshotReader { Page = emptyPage };
        var handler = CreateHandler(reader);

        var firstPage = await handler.HandleAsync(new("FX", 50, null, null, null, null), CancellationToken.None);
        reader.Page = null;
        var stalePage = await handler.HandleAsync(new("FX", 50, 5, "FX", "EPIC", "Demo"), CancellationToken.None);

        Assert.Equal(MarketCategoryInstrumentPageReadStatus.Page, firstPage.Status);
        Assert.Empty(firstPage.Page!.Instruments);
        Assert.Equal(MarketCategoryInstrumentPageReadStatus.StaleCursor, stalePage.Status);
    }

    private static GetMarketCategoryInstrumentPageHandler CreateHandler(SnapshotReader reader) =>
        new(
            new AppliedEnvironmentResolver(),
            new CategoryStore(),
            reader,
            new DetailReader());

    private sealed class AppliedEnvironmentResolver : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(
                Guid.NewGuid(),
                "IG",
                "Demo",
                "Active",
                "Available",
                "IgDemo",
                true,
                true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CategoryStore : IMarketCategorySnapshotStore
    {
        public Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult<MarketCategorySnapshot?>(new([new("FX", false)], DateTimeOffset.UtcNow, 1));

        public Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class SnapshotReader : IMarketCategoryInstrumentSnapshotReader
    {
        public int ReadCount { get; private set; }
        public MarketCategoryInstrumentSnapshotPageRequest? LastRequest { get; private set; }
        public MarketCategoryInstrumentSnapshotPage? Page { get; set; }

        public Task<MarketCategoryInstrumentSnapshotPage?> ReadPageAsync(
            MarketCategoryInstrumentSnapshotPageRequest request,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            LastRequest = request;
            return Task.FromResult(Page);
        }
    }

    private sealed class DetailReader : IMarketDetailReader
    {
        public Task<MarketDetailReadResult> ReadAsync(
            MarketDetailReadRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MarketDetailAvailabilityReadResult> ReadAvailabilityAsync(
            MarketDetailAvailabilityReadRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MarketDetailAvailabilityReadResult(
                true,
                true,
                request.ExpectedListingVersion,
                DateTimeOffset.UtcNow,
                new(
                    request.CategoryCode,
                    true,
                    MarketDetailRunStatus.NeverCollected,
                    null,
                    null,
                    null,
                    null),
                request.Epics.Select(epic => new MarketDetailAvailability(
                    epic,
                    true,
                    true,
                    MarketDetailTargetStatus.NotCollected,
                    null,
                    null)).ToArray()));

        public Task<IReadOnlyList<MarketDetailCategoryCoverage>> ReadCategoryCoverageAsync(
            BrokerEnvironmentKind appliedEnvironment,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
