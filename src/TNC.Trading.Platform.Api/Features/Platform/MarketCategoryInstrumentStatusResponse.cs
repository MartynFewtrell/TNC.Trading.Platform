namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Safe persisted collection status for the currently applied market-data environment.</summary>
internal sealed record MarketCategoryInstrumentStatusResponse(
    string State,
    string? BrokerEnvironment,
    DateOnly? TradingDay,
    bool IsDue,
    int? CurrentSlot,
    DateTimeOffset? NextWakeUpUtc,
    string? PauseReason,
    DateTimeOffset? LastCategoryRefreshAtUtc,
    int? LastCompletedSlot,
    string? CycleOutcome,
    string? CategoryPrerequisiteOutcome,
    string? SafeCategoryFailure,
    int UsedRequestBudget,
    int? ApprovedDailyRequestAllowance,
    IReadOnlyList<MarketCategoryCollectionStatusResponse> Categories);

/// <summary>Safe attempt status for one category's last saved collection.</summary>
internal sealed record MarketCategoryCollectionStatusResponse(
    string CategoryCode,
    DateTimeOffset? LastSuccessfulCollectionAtUtc,
    int Attempts,
    string? Outcome,
    string? SafeFailure);
