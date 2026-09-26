namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailRunStore
{
    Task<MarketDetailRunLease?> TryAcquireAsync(
        MarketDetailRunKey key,
        MarketDetailRevisions revisions,
        string appliedEndpointProfile,
        Guid owner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken);

    Task<MarketDetailRunStatus> StageUniverseAsync(
        MarketDetailRunLease lease,
        MarketDetailUniverse universe,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketDetailTarget>> ReadOutstandingTargetsAsync(
        MarketDetailRunLease lease,
        int maximumCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketDetailCapacityTarget>> ReadRetryableCapacityTargetsAsync(
        MarketDetailRunLease lease,
        CancellationToken cancellationToken);

    Task<bool> RecordFailureAsync(
        MarketDetailRunLease lease,
        MarketDetailGatewayResult result,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken);

    Task<MarketDetailRunCounts> ReadCountsAsync(
        MarketDetailRunLease lease,
        CancellationToken cancellationToken);

    Task<MarketDetailRunStatus> FinalizeAsync(
        MarketDetailRunLease lease,
        bool prerequisitesValidated,
        bool revisionsCurrent,
        bool capacityAvailable,
        bool isRunning,
        CancellationToken cancellationToken);
}
