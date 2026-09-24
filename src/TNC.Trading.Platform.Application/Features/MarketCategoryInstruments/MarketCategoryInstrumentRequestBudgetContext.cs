namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Lease identity required to reserve provider calls from the durable per-environment allowance.</summary>
internal sealed record MarketCategoryInstrumentRequestBudgetContext(
    DateOnly TradingDay,
    int ScheduledSlot,
    Guid LeaseOwner,
    long LeaseFence,
    CancellationToken ScheduleCancellationToken = default,
    DateTimeOffset? ScheduleWindowEndUtc = null,
    long ScheduleRevision = 0,
    int EffectiveUpdatesPerDay = 1,
    string? AppliedEndpointProfile = null,
    bool IsManualOperation = false);
