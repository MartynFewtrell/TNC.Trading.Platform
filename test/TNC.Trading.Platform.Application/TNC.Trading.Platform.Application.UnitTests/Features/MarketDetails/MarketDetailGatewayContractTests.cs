using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketDetails;

public sealed class MarketDetailGatewayContractTests
{
    /// <summary>
    /// Trace: Market Details Work Item 1, step 2.
    /// Verifies: provider endpoint family and explicit response version remain paired.
    /// Expected: a bulk-v2 source cannot be represented as v3/v4 data.
    /// Why: single-market v3/v4 response shapes must never be silently interpreted as bulk-v2 observations.
    /// </summary>
    [Fact]
    public void Observation_ShouldRejectMismatchedEndpointVersion_WhenConstructed()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(sourceVersion: 3));

        Assert.Contains("source and provider version", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 2.
    /// Verifies: validated observation identity is the exact requested EPIC and its retrieval instant is UTC.
    /// Expected: a local-offset timestamp or a different instrument EPIC cannot form a validated observation.
    /// Why: provider timestamps are not interchangeable with platform retrieval UTC and batch mismatches must fail closed.
    /// </summary>
    [Fact]
    public void Observation_ShouldRejectNonUtcRetrievalTime_WhenConstructed()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateObservation(retrievedAtUtc: new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(1))));

        Assert.Contains("must be UTC", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 2.
    /// Verifies: a gateway response associates one result with exactly one matching requested EPIC.
    /// Expected: a validated observation with a different EPIC is rejected by the result contract.
    /// Why: unexpected or mismatched markets must not be published under another target's identity.
    /// </summary>
    [Fact]
    public void Observation_ShouldRejectMismatchedInstrumentEpic_WhenConstructed()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateObservation(instrumentEpic: "CS.D.WRONG.CFD.IP"));

        Assert.Contains("must match", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 2; Non-negotiable Behaviour 4.
    /// Verifies: bulk requests enforce IG's documented fifty-EPIC maximum and reject duplicate identities.
    /// Expected: 50 unique targets up to 64 characters are accepted, while 51 targets, overlength targets, and duplicates are rejected.
    /// Why: bounded requests preserve URL, allowance, and exact per-target accounting.
    /// </summary>
    [Fact]
    public void GatewayRequest_ShouldEnforceBulkBatchBounds_AndUniqueEpics()
    {
        var context = new MarketDetailRequestBudgetContext(
            Guid.NewGuid(),
            BrokerEnvironmentKind.Demo,
            new DateOnly(2026, 9, 25),
            0,
            Guid.NewGuid(),
            1,
            1,
            1,
            "IgDemo",
            new DateTimeOffset(2026, 9, 25, 18, 0, 0, TimeSpan.Zero));

        var maximumBatch = Enumerable.Range(0, 50).Select(index => $"EPIC{index:00}").ToArray();
        var request = new MarketDetailGatewayRequest(maximumBatch, context);

        Assert.Equal(50, request.Epics.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MarketDetailGatewayRequest(Enumerable.Range(0, 51).Select(index => $"EPIC{index:00}").ToArray(), context));
        Assert.Throws<ArgumentException>(() =>
            new MarketDetailGatewayRequest(["CS.D.ADAUSD.CFD.IP", "CS.D.ADAUSD.CFD.IP"], context));
        Assert.Throws<ArgumentException>(() =>
            new MarketDetailGatewayRequest([new string('E', 65)], context));
    }

    private static MarketDetailValidatedObservation CreateObservation(
        int sourceVersion = 2,
        DateTimeOffset? retrievedAtUtc = null,
        string? instrumentEpic = null)
    {
        var epic = "CS.D.ADAUSD.CFD.IP";
        var instrument = new MarketDetailInstrument(
            instrumentEpic ?? epic,
            "-",
            "Cardano ($1)",
            "ADAUSD",
            "CURRENCIES",
            "CONTRACTS",
            1m,
            true,
            true,
            true,
            true,
            [],
            [],
            100m,
            "PERCENTAGE",
            MarketDetailQuantity.FromValue(100m, "pct"),
            MarketDetailQuantity.FromValue(0.7m, "POINTS"),
            MarketDetailQuantity.ExplicitNull(),
            MarketDetailQuantity.ExplicitNull(),
            null,
            null,
            null,
            "ADA=",
            null,
            null,
            "1.00",
            "0.01",
            "100",
            ["Quoted 24/7"]);
        var rules = new MarketDetailDealingRules(
            MarketDetailQuantity.FromValue(5m, "POINTS"),
            MarketDetailQuantity.FromValue(75m, "PERCENTAGE"),
            MarketDetailQuantity.FromValue(10m, "PERCENTAGE"),
            MarketDetailQuantity.FromValue(0.1m, "POINTS"),
            MarketDetailQuantity.FromValue(1m, "POINTS"),
            MarketDetailQuantity.FromValue(1m, "POINTS"),
            "AVAILABLE_DEFAULT_OFF",
            "NOT_AVAILABLE");
        var snapshot = new MarketDetailMarketSnapshot(
            "TRADEABLE",
            MarketDetailQuantity.FromValue(0.64m),
            MarketDetailQuantity.FromValue(2.59m),
            "17:56:59",
            MarketDetailQuantity.FromValue(0m),
            MarketDetailQuantity.FromValue(25.33m),
            MarketDetailQuantity.FromValue(25.43m),
            MarketDetailQuantity.FromValue(25.90m),
            MarketDetailQuantity.FromValue(24.60m),
            MarketDetailQuantity.ExplicitNull(),
            MarketDetailQuantity.FromValue(2m),
            MarketDetailQuantity.FromValue(1m),
            MarketDetailQuantity.ExplicitNull());

        return new(
            epic,
            retrievedAtUtc ?? new DateTimeOffset(2026, 9, 25, 17, 57, 0, TimeSpan.Zero),
            "/markets",
            sourceVersion,
            MarketDetailObservationSource.BulkV2,
            "17:56:59",
            instrument,
            rules,
            snapshot);
    }
}
