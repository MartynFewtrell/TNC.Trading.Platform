namespace TNC.Trading.Platform.Web;

internal sealed record MarketCategoryInstrumentCollectionStatusViewModel(
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
    IReadOnlyList<MarketCategoryCollectionStatusViewModel> Categories);
