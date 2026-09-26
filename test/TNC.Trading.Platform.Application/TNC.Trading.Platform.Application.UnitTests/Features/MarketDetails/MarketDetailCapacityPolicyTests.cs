using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketDetails;

public sealed class MarketDetailCapacityPolicyTests
{
    /// <summary>
    /// Trace: Market Details Work Item 4, step 2.
    /// Verifies: a 384-EPIC frozen universe is budgeted as bounded batches of at most 50, with a session and one possible 401 replay reserved for each request.
    /// Expected: the immediate no-retry plan is 8 market batches plus 8 sessions, and the three-attempt worst case is 72 requests.
    /// Why: collection must not claim capacity based on a single-market fallback or an unbounded bulk request.
    /// </summary>
    [Fact]
    public void Estimate_ShouldBoundBulkBatchesAndReserveRetries_WhenUniverseHas384Targets()
    {
        var targets = Enumerable.Range(0, 384)
            .Select(index => new MarketDetailCapacityTarget($"CS.D.TEST{index:D4}.CFD.IP", 0))
            .ToArray();

        var estimate = new MarketDetailCapacityPolicy().Estimate(targets);

        Assert.Equal(16, estimate.NoRetryRequests);
        Assert.Equal(56, estimate.RetryAndReauthenticationReserve);
        Assert.Equal(72, estimate.WorstCaseRequests);
        Assert.Equal(24, estimate.MarketBatches);
    }

    /// <summary>
    /// Trace: Market Details Work Item 7, capacity evidence.
    /// Verifies: the current 15,000-instrument category bound is converted to 300 first-attempt bulk calls, with bounded retries and 401 reauthentication replays.
    /// Expected: the no-retry estimate is 600 requests and the three-attempt worst-case estimate is 2,700 requests.
    /// Why: the listing cap and 50-target request cap must not be mistaken for proof that the configured daily allowance or active window can serve a full universe.
    /// </summary>
    [Fact]
    public void Estimate_ShouldExposeRequestCost_WhenUniverseHas15000Targets()
    {
        var targets = Enumerable.Range(0, 15_000)
            .Select(index => new MarketDetailCapacityTarget($"CS.D.TEST{index:D8}.CFD.IP", 0))
            .ToArray();

        var estimate = new MarketDetailCapacityPolicy().Estimate(targets);

        Assert.Equal(900, estimate.MarketBatches);
        Assert.Equal(600, estimate.NoRetryRequests);
        Assert.Equal(2_100, estimate.RetryAndReauthenticationReserve);
        Assert.Equal(2_700, estimate.WorstCaseRequests);
    }

    /// <summary>
    /// Trace: Market Details Work Item 4, step 2.
    /// Verifies: a target already attempted twice reserves only its next attempt and includes that attempt in the no-retry cost.
    /// Expected: one session plus one market request is the immediate cost and one 401 replay is the only additional reserve.
    /// Why: resumed runs must estimate the remaining work rather than incorrectly reporting zero baseline capacity.
    /// </summary>
    [Fact]
    public void Estimate_ShouldCountImmediateCostForPreviouslyAttemptedTargets_WhenResuming()
    {
        var estimate = new MarketDetailCapacityPolicy().Estimate(
            [new("CS.D.ADAUSD.CFD.IP", 2)]);

        Assert.Equal(2, estimate.NoRetryRequests);
        Assert.Equal(1, estimate.RetryAndReauthenticationReserve);
        Assert.Equal(3, estimate.WorstCaseRequests);
        Assert.Equal(1, estimate.MarketBatches);
    }
}
