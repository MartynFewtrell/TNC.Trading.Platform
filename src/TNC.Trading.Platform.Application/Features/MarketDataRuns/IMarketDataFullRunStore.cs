using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal interface IMarketDataFullRunStore : IMarketDataFullRunIntentWriter
{
    Task<MarketDataFullRunAdmissionResult> TryAdmitAsync(
        MarketDataFullRunAdmissionRequest request,
        CancellationToken cancellationToken);

    Task<bool> TryRenewLeaseAsync(
        MarketDataFullRunLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> RecordSlotCoverageAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunSlotIdentity slot,
        DateTimeOffset coveredAtUtc,
        CancellationToken cancellationToken);

    Task<bool> RecordStageAttemptAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        string status,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeReasonCode,
        CancellationToken cancellationToken);

    Task<bool> RecordItemAttemptAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        string itemCode,
        string status,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeReasonCode,
        CancellationToken cancellationToken);

    Task<string?> GetStageStatusAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetSucceededItemsAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<bool> CompleteAsync(
        MarketDataFullRunLease lease,
        string outcome,
        string? safeReasonCode,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task<MarketDataFullRunIntent?> GetPendingIntentAsync(
        BrokerEnvironmentKind environment,
        CancellationToken cancellationToken);

    Task<MarketDataFailedItemRetry?> GetPendingFailedItemRetryAsync(
        BrokerEnvironmentKind environment,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<MarketDataFailedItemRetry?>(null);
}
