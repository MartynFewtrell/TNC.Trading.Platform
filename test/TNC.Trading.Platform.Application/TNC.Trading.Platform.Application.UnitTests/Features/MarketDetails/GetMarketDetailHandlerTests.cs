using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketDetails;

public sealed class GetMarketDetailHandlerTests
{
    /// <summary>
    /// Trace: Market Details Work Item 5, step 2.
    /// Verifies: a detail request whose listing precondition no longer matches is returned as a stale-version outcome.
    /// Expected: the handler reports StaleListingVersion and forwards the caller's expected version to the SQL reader.
    /// Why: direct links must not silently switch to a different current listing membership.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnStaleVersion_WhenListingVersionHasChanged()
    {
        var reader = new DetailReader(ReadResult(currentMembershipExists: true, listingVersionMatches: false));
        var handler = new GetMarketDetailHandler(new AppliedEnvironmentResolver(CreateDemoContext()), reader);

        var response = await handler.HandleAsync(
            new("FX", "CS.D.ADAUSD.CFD.IP", 3),
            CancellationToken.None);

        Assert.Equal(GetMarketDetailStatus.StaleListingVersion, response.Status);
        Assert.Equal(3, reader.LastRequest!.ExpectedListingVersion);
        Assert.Equal(BrokerEnvironmentKind.Demo, reader.LastRequest.AppliedEnvironment);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, steps 2 and 4.
    /// Verifies: a supported request with no current category/EPIC membership produces an explicit not-found result.
    /// Expected: the handler reports CurrentMembershipNotFound and does not return a fabricated detail object.
    /// Why: old historical observations must not bypass the current applied-environment listing scope.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnNotFound_WhenCurrentMembershipIsAbsent()
    {
        var reader = new DetailReader(ReadResult(currentMembershipExists: false, listingVersionMatches: true));
        var handler = new GetMarketDetailHandler(new AppliedEnvironmentResolver(CreateDemoContext()), reader);

        var response = await handler.HandleAsync(
            new("FX", "CS.D.ADAUSD.CFD.IP", null),
            CancellationToken.None);

        Assert.Equal(GetMarketDetailStatus.CurrentMembershipNotFound, response.Status);
        Assert.Null(response.Detail);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, step 2.
    /// Verifies: an unsupported applied environment is reported before the reader can access persisted market detail.
    /// Expected: the handler returns AppliedEnvironmentUnavailable and performs no SQL-reader call.
    /// Why: every read must honor the active environment boundary without falling back to Demo or Live data.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnUnavailable_WhenAppliedEnvironmentIsUnsupported()
    {
        var reader = new DetailReader(ReadResult(currentMembershipExists: true, listingVersionMatches: true));
        var handler = new GetMarketDetailHandler(new AppliedEnvironmentResolver(null), reader);

        var response = await handler.HandleAsync(
            new("FX", "CS.D.ADAUSD.CFD.IP", null),
            CancellationToken.None);

        Assert.Equal(GetMarketDetailStatus.AppliedEnvironmentUnavailable, response.Status);
        Assert.Equal(0, reader.ReadCount);
    }

    private static MarketDetailReadResult ReadResult(bool currentMembershipExists, bool listingVersionMatches) =>
        new(
            currentMembershipExists,
            listingVersionMatches,
            2,
            DateTimeOffset.UtcNow,
            true,
            MarketDetailTargetStatus.NotCollected,
            null,
            MarketDetailRunStatus.NeverCollected,
            null,
            null,
            null,
            null,
            null);

    private static AppliedBrokerEnvironmentContext CreateDemoContext() =>
        new(Guid.NewGuid(), "IG", "Demo", "Active", "Available", "IgDemo", true, true);

    private sealed class AppliedEnvironmentResolver(AppliedBrokerEnvironmentContext? context) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            Task.FromResult(context);
    }

    private sealed class DetailReader(MarketDetailReadResult readResult) : IMarketDetailReader
    {
        public int ReadCount { get; private set; }

        public MarketDetailReadRequest? LastRequest { get; private set; }

        public Task<MarketDetailReadResult> ReadAsync(
            MarketDetailReadRequest request,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            LastRequest = request;
            return Task.FromResult(readResult);
        }

        public Task<MarketDetailAvailabilityReadResult> ReadAvailabilityAsync(
            MarketDetailAvailabilityReadRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MarketDetailCategoryCoverage>> ReadCategoryCoverageAsync(
            BrokerEnvironmentKind appliedEnvironment,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
