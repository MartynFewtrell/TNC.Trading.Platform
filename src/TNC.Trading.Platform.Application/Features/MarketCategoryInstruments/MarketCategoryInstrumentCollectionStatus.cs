using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Persisted cycle and category progress suitable for a protected status surface.</summary>
internal sealed record MarketCategoryInstrumentCollectionStatus(
    BrokerEnvironmentKind BrokerEnvironment,
    DateOnly TradingDay,
    DateTimeOffset? LastCategoryRefreshAtUtc,
    int? LastCompletedSlot,
    string? CycleOutcome,
    string? CategoryPrerequisiteOutcome,
    string? SafeCategoryFailure,
    int UsedRequestBudget,
    int? ApprovedDailyRequestAllowance,
    IReadOnlyList<MarketCategoryInstrumentCategoryStatus> Categories);
