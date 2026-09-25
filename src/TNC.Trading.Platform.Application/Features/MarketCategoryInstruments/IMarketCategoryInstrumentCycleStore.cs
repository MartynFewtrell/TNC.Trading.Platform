using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Durable, SQL-backed coordination for a schedule slot and its independent category attempts.</summary>
internal interface IMarketCategoryInstrumentCycleStore
{
    Task<MarketCategoryInstrumentSlotProgress?> GetLatestProgressAsync(
        BrokerEnvironmentKind environment,
        CancellationToken cancellationToken);

    Task<long?> TryAcquireLeaseAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> TryRenewLeaseAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> TryBeginCategoryPrerequisiteAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<bool> TryReserveCategoryAttemptAsync(
        MarketCategoryInstrumentCycleLease lease,
        string categoryCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<bool> CompleteCategoryPrerequisiteAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeError,
        CancellationToken cancellationToken);

    Task<bool> CompleteCategoryAttemptAsync(
        MarketCategoryInstrumentCycleLease lease,
        string categoryCode,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeError,
        CancellationToken cancellationToken);

    Task<bool> HasRequestBudgetAsync(
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken cancellationToken);

    Task<bool> TryConsumeManualRequestBudgetAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        long scheduleRevision,
        DateTimeOffset nowUtc,
        DateTimeOffset windowEndUtc,
        int requestCount,
        CancellationToken cancellationToken);

    Task<bool> CompleteCycleAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        string outcome,
        CancellationToken cancellationToken);

    Task RecordMissedSlotsAsync(
        MarketCategoryInstrumentCycleLease lease,
        IReadOnlyList<int> missedSlotIndexes,
        CancellationToken cancellationToken);
}
