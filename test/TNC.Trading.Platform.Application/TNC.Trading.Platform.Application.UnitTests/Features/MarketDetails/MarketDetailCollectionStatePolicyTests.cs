using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketDetails;

public sealed class MarketDetailCollectionStatePolicyTests
{
    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: complete empty coverage is possible only after validated prerequisites and current revisions.
    /// Expected: a zero-target run resolves to Complete when its sources are validated and there is no outstanding target.
    /// Why: an empty interested universe is different from a failed prerequisite or an uninitialized run.
    /// </summary>
    [Fact]
    public void ResolveRunStatus_ShouldComplete_WhenValidatedUniverseIsEmpty()
    {
        var status = new MarketDetailCollectionStatePolicy().ResolveRunStatus(
            runExists: true,
            isRunning: false,
            prerequisitesValidated: true,
            revisionsCurrent: true,
            capacityAvailable: true,
            new MarketDetailRunCounts(0, 0, 0));

        Assert.Equal(MarketDetailRunStatus.Complete, status);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: a failed source prerequisite blocks full coverage regardless of individual successes.
    /// Expected: the run remains Blocked even when every currently expected target has an observation.
    /// Why: last-good or partial data must never imply that the current selected universe was fully collected.
    /// </summary>
    [Fact]
    public void ResolveRunStatus_ShouldBlock_WhenListingPrerequisiteFailed()
    {
        var status = new MarketDetailCollectionStatePolicy().ResolveRunStatus(
            runExists: true,
            isRunning: false,
            prerequisitesValidated: false,
            revisionsCurrent: true,
            capacityAvailable: true,
            new MarketDetailRunCounts(1, 1, 0));

        Assert.Equal(MarketDetailRunStatus.Blocked, status);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: revision drift supersedes a frozen run without discarding completed target observations.
    /// Expected: the aggregate state is Superseded while target-level history remains readable.
    /// Why: current-universe completeness cannot be claimed against a changed catalogue, interest, schedule, or profile.
    /// </summary>
    [Fact]
    public void ResolveRunStatus_ShouldSupersede_WhenFrozenRevisionsAreStale()
    {
        var policy = new MarketDetailCollectionStatePolicy();
        var runStatus = policy.ResolveRunStatus(
            runExists: true,
            isRunning: false,
            prerequisitesValidated: true,
            revisionsCurrent: false,
            capacityAvailable: true,
            new MarketDetailRunCounts(2, 1, 0));
        var targetStatus = policy.ResolveTargetStatus(
            providerConfirmedExcluded: false,
            hasLastGoodObservation: true,
            observedInCurrentRun: true);

        Assert.Equal(MarketDetailRunStatus.Superseded, runStatus);
        Assert.Equal(MarketDetailTargetStatus.Complete, targetStatus);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3; Non-negotiable Behaviour 5.
    /// Verifies: an individually valid observation remains Complete even when aggregate collection is incomplete.
    /// Expected: current-run observation state is independent of aggregate status.
    /// Why: valid peer markets must remain inspectable without making the full run analysis-ready.
    /// </summary>
    [Fact]
    public void ResolveTargetStatus_ShouldKeepCurrentObservationComplete_WhenRunHasOutstandingTargets()
    {
        var status = new MarketDetailCollectionStatePolicy().ResolveTargetStatus(
            providerConfirmedExcluded: false,
            hasLastGoodObservation: true,
            observedInCurrentRun: true);

        Assert.Equal(MarketDetailTargetStatus.Complete, status);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: last-good details from a previous run are out of date until observed in the current run.
    /// Expected: an older observation is OutOfDate rather than Complete.
    /// Why: aggregate analysis must not mix values from different frozen slots.
    /// </summary>
    [Fact]
    public void ResolveTargetStatus_ShouldMarkLastGoodOutOfDate_WhenCurrentRunHasNoObservation()
    {
        var status = new MarketDetailCollectionStatePolicy().ResolveTargetStatus(
            providerConfirmedExcluded: false,
            hasLastGoodObservation: true,
            observedInCurrentRun: false);

        Assert.Equal(MarketDetailTargetStatus.OutOfDate, status);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: only EPIC-specific confirmed unavailability can exclude a frozen target and positive evidence reinstates it.
    /// Expected: transient, ambiguous, and later-positive evidence do not create an exclusion.
    /// Why: malformed batches and temporary provider failures must not permanently remove valid instruments.
    /// </summary>
    [Theory]
    [InlineData(nameof(MarketDetailTargetFailureKind.ProviderConfirmedUnavailable), true, false, true)]
    [InlineData(nameof(MarketDetailTargetFailureKind.ProviderConfirmedUnavailable), false, false, false)]
    [InlineData(nameof(MarketDetailTargetFailureKind.TransientProviderFailure), true, false, false)]
    [InlineData(nameof(MarketDetailTargetFailureKind.RateLimited), true, false, false)]
    [InlineData(nameof(MarketDetailTargetFailureKind.ProviderConfirmedUnavailable), true, true, false)]
    public void ShouldExcludeTarget_ShouldRequireSpecificConfirmedEvidence_AndReinstateOnPositiveEvidence(
        string failureKindName,
        bool failureIdentifiesEpic,
        bool positiveProviderEvidence,
        bool expected)
    {
        var failureKind = Enum.Parse<MarketDetailTargetFailureKind>(failureKindName);
        var result = new MarketDetailCollectionStatePolicy().ShouldExcludeTarget(
            failureKind,
            failureIdentifiesEpic,
            positiveProviderEvidence);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3; Non-negotiable Behaviour 3.
    /// Verifies: the run-count denominator excludes confirmed exclusions and failed targets remain outstanding.
    /// Expected: frozen count is expected plus excluded and outstanding is expected less completed.
    /// Why: failures are a subset of outstanding work, not a second denominator.
    /// </summary>
    [Fact]
    public void RunCounts_ShouldDeriveFrozenAndOutstandingCounts_FromExpectedCompletedAndExcluded()
    {
        var counts = new MarketDetailRunCounts(expectedCount: 4, completedCount: 2, excludedCount: 1);

        Assert.Equal(5, counts.FrozenCount);
        Assert.Equal(2, counts.OutstandingCount);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: nullable provider values preserve the distinction between absence, explicit null, and numeric zero.
    /// Expected: each state round-trips distinctly and a supplied zero remains a value.
    /// Why: rendering and analysis must not replace zero with a missing-value label.
    /// </summary>
    [Fact]
    public void Quantity_ShouldPreserveAbsenceNullAndZero_AsDistinctStates()
    {
        var absent = MarketDetailQuantity.NotSupplied("POINTS");
        var explicitNull = MarketDetailQuantity.ExplicitNull();
        var zero = MarketDetailQuantity.FromValue(0m, "POINTS");

        Assert.Equal(MarketDetailValuePresence.NotSupplied, absent.Presence);
        Assert.Equal(MarketDetailValuePresence.ExplicitNull, explicitNull.Presence);
        Assert.Equal(MarketDetailValuePresence.Value, zero.Presence);
        Assert.Equal(0m, zero.Value);
        Assert.Equal("POINTS", zero.Unit);
    }
}
