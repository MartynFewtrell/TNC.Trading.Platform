using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class MarketDetailEndpointMappingTests
{
    /// <summary>
    /// Trace: Market Details Work Item 5, steps 1 and 2.
    /// Verifies: a known current EPIC without an observation returns a successful, explicit NotCollected response with separate listing provenance.
    /// Expected: HTTP 200 includes the current listing time, null detail observation, and independent aggregate coverage.
    /// Why: a valid but not-yet-collected instrument must remain discoverable without implying that nested detail data exists.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldReturnNotCollectedDetail_WhenMembershipExistsWithoutObservation()
    {
        var listingRetrievedAtUtc = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var response = FoundResponse(new(
            true,
            true,
            4,
            listingRetrievedAtUtc,
            true,
            MarketDetailTargetStatus.NotCollected,
            null,
            MarketDetailRunStatus.NeverCollected,
            null,
            null,
            null,
            null,
            null));

        var result = Assert.IsType<Ok<MarketDetailResponse>>(response.ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("NotCollected", result.Value!.State);
        Assert.Equal(listingRetrievedAtUtc, result.Value.ListingRetrievedAtUtc);
        Assert.Null(result.Value.SavedObservation);
        Assert.Equal("NeverCollected", result.Value.Coverage.State);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, steps 1 and 2.
    /// Verifies: the saved-detail API mapping preserves typed provider sections, source provenance, separate UTC retrieval times, nullable units, and string value-presence states.
    /// Expected: the response carries the saved v2 source and retains an explicit null margin ceiling distinctly from a missing value.
    /// Why: consumers need the validated complete `filter=ALL` data without receiving raw headers, credentials, or provider error text.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldPreserveSavedSectionsAndProvenance_WhenObservationExists()
    {
        var listingRetrievedAtUtc = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var detailRetrievedAtUtc = listingRetrievedAtUtc.AddSeconds(5);
        var result = Assert.IsType<Ok<MarketDetailResponse>>(
            FoundResponse(new(
                true,
                true,
                4,
                listingRetrievedAtUtc,
                true,
                MarketDetailTargetStatus.Complete,
                CreateObservation(detailRetrievedAtUtc),
                MarketDetailRunStatus.Complete,
                new(1, 1, 0),
                detailRetrievedAtUtc,
                null,
                null,
                null)).ToHttpResult());

        Assert.Equal(listingRetrievedAtUtc, result.Value!.ListingRetrievedAtUtc);
        Assert.Equal(detailRetrievedAtUtc, result.Value.SavedObservation!.RetrievedAtUtc);
        Assert.Equal("BulkV2", result.Value.SavedObservation.Source);
        Assert.Equal("/markets?filter=ALL", result.Value.SavedObservation.SourceEndpoint);
        Assert.Equal(2, result.Value.SavedObservation.SourceVersion);
        Assert.Equal("USD", Assert.Single(result.Value.SavedObservation.Instrument.Currencies).Code);
        Assert.Equal("ExplicitNull", Assert.Single(result.Value.SavedObservation.Instrument.MarginDepositBands).Max.Presence);
        Assert.Equal("Value", result.Value.SavedObservation.Snapshot.Bid.Presence);
        Assert.Equal(25.33m, result.Value.SavedObservation.Snapshot.Bid.Value);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, steps 2 and 4.
    /// Verifies: absent current membership is mapped to HTTP 404 rather than a fabricated empty detail object.
    /// Expected: the response is Problem Details with status 404.
    /// Why: direct links must be scoped to current category/EPIC membership in the applied environment.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldReturnNotFound_WhenCurrentMembershipIsAbsent()
    {
        var result = Assert.IsType<ProblemHttpResult>(
            new GetMarketDetailResponse(GetMarketDetailStatus.CurrentMembershipNotFound, BrokerEnvironmentKind.Demo, "FX", "MISSING", null)
                .ToHttpResult());

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, steps 2 and 4.
    /// Verifies: a stale caller-supplied listing version does not return an observation from a newer category snapshot.
    /// Expected: the response is Problem Details with status 409.
    /// Why: a deep link can detect listing drift and ask the client to reload rather than silently changing membership context.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConflict_WhenListingVersionIsStale()
    {
        var result = Assert.IsType<ProblemHttpResult>(
            new GetMarketDetailResponse(GetMarketDetailStatus.StaleListingVersion, BrokerEnvironmentKind.Demo, "FX", "EPIC", null)
                .ToHttpResult());

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, steps 2 and 4.
    /// Verifies: an unsupported applied environment returns a safe service-unavailable problem.
    /// Expected: the response is Problem Details with status 503 and no internal failure detail.
    /// Why: unsupported runtime configuration must be surfaced explicitly without leaking provider or credential data.
    /// </summary>
    [Fact]
    public void ToHttpResult_ShouldReturnServiceUnavailable_WhenAppliedEnvironmentIsUnsupported()
    {
        var result = Assert.IsType<ProblemHttpResult>(
            new GetMarketDetailResponse(GetMarketDetailStatus.AppliedEnvironmentUnavailable, null, "FX", "EPIC", null)
                .ToHttpResult());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.DoesNotContain("credential", result.ProblemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static GetMarketDetailResponse FoundResponse(MarketDetailReadResult detail) =>
        new(GetMarketDetailStatus.Found, BrokerEnvironmentKind.Demo, "FX", "EPIC", detail);

    private static MarketDetailValidatedObservation CreateObservation(DateTimeOffset retrievedAtUtc)
    {
        var instrument = new MarketDetailInstrument(
            "EPIC",
            "-",
            "Cardano",
            "ADAUSD",
            "CURRENCIES",
            "CONTRACTS",
            1m,
            true,
            true,
            true,
            true,
            [new("USD", "$", 1.324262m, 0.66m, false)],
            [new(0m, MarketDetailQuantity.ExplicitNull("USD"), 100m, "USD")],
            100m,
            "PERCENTAGE",
            MarketDetailQuantity.FromValue(100m, "pct"),
            MarketDetailQuantity.ExplicitNull("POINTS"),
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
        var dealingRules = new MarketDetailDealingRules(
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
            "EPIC",
            retrievedAtUtc,
            "/markets?filter=ALL",
            2,
            MarketDetailObservationSource.BulkV2,
            "17:56:59",
            instrument,
            dealingRules,
            snapshot);
    }
}
