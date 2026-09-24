namespace TNC.Trading.Platform.Web;

internal sealed record InstrumentCollectionConfigurationViewModel(
    bool IsAvailable,
    string? Status,
    string? AppliedBrokerEnvironment,
    int? CurrentUpdatesPerDay,
    int? PendingUpdatesPerDay,
    DateOnly? PendingEffectiveTradingDay,
    int? ApprovedNonTradingDailyRequestAllowance,
    int? UsedRequestBudget,
    string? CollectionOutcome,
    string? SafeFailure);
