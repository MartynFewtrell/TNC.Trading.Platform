namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed class MarketDetailCollectionStatePolicy
{
    public MarketDetailRunStatus ResolveRunStatus(
        bool runExists,
        bool isRunning,
        bool prerequisitesValidated,
        bool revisionsCurrent,
        bool capacityAvailable,
        MarketDetailRunCounts counts)
    {
        if (!runExists)
        {
            return MarketDetailRunStatus.NeverCollected;
        }

        if (!revisionsCurrent)
        {
            return MarketDetailRunStatus.Superseded;
        }

        if (!prerequisitesValidated || !capacityAvailable)
        {
            return MarketDetailRunStatus.Blocked;
        }

        if (counts.OutstandingCount == 0)
        {
            return MarketDetailRunStatus.Complete;
        }

        return isRunning
            ? MarketDetailRunStatus.Running
            : MarketDetailRunStatus.Incomplete;
    }

    public MarketDetailTargetStatus ResolveTargetStatus(
        bool providerConfirmedExcluded,
        bool hasLastGoodObservation,
        bool observedInCurrentRun) =>
        providerConfirmedExcluded
            ? MarketDetailTargetStatus.Excluded
            : observedInCurrentRun
                ? MarketDetailTargetStatus.Complete
                : hasLastGoodObservation
                    ? MarketDetailTargetStatus.OutOfDate
                    : MarketDetailTargetStatus.NotCollected;

    public bool ShouldExcludeTarget(
        MarketDetailTargetFailureKind failureKind,
        bool failureIdentifiesEpic,
        bool positiveProviderEvidence) =>
        !positiveProviderEvidence
        && failureIdentifiesEpic
        && failureKind == MarketDetailTargetFailureKind.ProviderConfirmedUnavailable;
}
