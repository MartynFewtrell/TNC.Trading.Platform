using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketCategoryInstruments;

public sealed class UpdateMarketCategoryInterestHandlerTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 2.
    /// Verifies: a stale environment-wide interest revision is returned as a conflict without attempting to write.
    /// Expected: the response contains the current revision and the writer remains untouched.
    /// Why: concurrent operators must not silently overwrite each other's category selections.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnCurrentRevisionWithoutWriting_WhenExpectedRevisionIsStale()
    {
        var reader = new InterestReader(new(4, [new("FX", false)]));
        var writer = new InterestWriter();
        var handler = CreateHandler(reader, writer);

        var response = await handler.HandleAsync(new("FX", true, 3), CancellationToken.None);

        Assert.Equal(UpdateMarketCategoryInterestStatus.RevisionConflict, response.Status);
        Assert.Equal(4, response.Revision);
        Assert.Empty(writer.SavedInterestSets);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 2.
    /// Verifies: a successful interest update changes only the requested current category and advances the shared revision.
    /// Expected: the writer receives the complete set with the requested category selected and returns its new revision.
    /// Why: the persisted interest list is environment-wide and must remain consistent under set-level compare-and-swap.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldSaveUpdatedSet_WhenRevisionAndCategoryAreCurrent()
    {
        var reader = new InterestReader(new(4, [new("FX", false), new("Equities", true)]));
        var writer = new InterestWriter { SavedRevision = 5 };
        var handler = CreateHandler(reader, writer);

        var response = await handler.HandleAsync(new("FX", true, 4), CancellationToken.None);

        Assert.Equal(UpdateMarketCategoryInterestStatus.Saved, response.Status);
        Assert.Equal(5, response.Revision);
        var saved = Assert.Single(writer.SavedInterestSets);
        Assert.Equal([new("FX", true), new("Equities", true)], saved);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 2.
    /// Verifies: additions for a category absent from the exact saved catalogue do not create an interest row.
    /// Expected: the handler reports CategoryNotFound and neither interest lookup nor persistence runs.
    /// Why: interest is limited to current provider category membership and must not create dormant unknown categories.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectUnknownCategory_BeforeReadingOrWritingInterest()
    {
        var reader = new InterestReader(new(4, []));
        var writer = new InterestWriter();
        var handler = CreateHandler(reader, writer, categoryCode: "FX");

        var response = await handler.HandleAsync(new("Unknown", true, 4), CancellationToken.None);

        Assert.Equal(UpdateMarketCategoryInterestStatus.CategoryNotFound, response.Status);
        Assert.Equal(0, reader.ReadCount);
        Assert.Empty(writer.SavedInterestSets);
    }

    private static UpdateMarketCategoryInterestHandler CreateHandler(
        InterestReader reader,
        InterestWriter writer,
        string categoryCode = "FX") =>
        new(
            new AppliedEnvironmentResolver(),
            new CategoryStore(categoryCode),
            reader,
            writer);

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

    private sealed class CategoryStore(string categoryCode) : IMarketCategorySnapshotStore
    {
        public Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult<MarketCategorySnapshot?>(new([new(categoryCode, false)], DateTimeOffset.UtcNow, 1));

        public Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class InterestReader(MarketCategoryInstrumentInterestState state) : IMarketCategoryInstrumentInterestReader
    {
        public int ReadCount { get; private set; }

        public Task<MarketCategoryInstrumentInterestState> ReadAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(state);
        }
    }

    private sealed class InterestWriter : IMarketCategoryInstrumentInterestWriter
    {
        public long SavedRevision { get; init; } = 5;
        public List<IReadOnlyList<MarketCategoryInstrumentInterest>> SavedInterestSets { get; } = [];

        public Task<long> SaveAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            IReadOnlyList<MarketCategoryInstrumentInterest> interests,
            long expectedRevision,
            CancellationToken cancellationToken)
        {
            SavedInterestSets.Add(interests);
            return Task.FromResult(SavedRevision);
        }
    }
}
