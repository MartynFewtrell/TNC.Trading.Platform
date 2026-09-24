namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

/// <summary>Environment-scoped collector settings safe for the operator configuration surface.</summary>
internal sealed record InstrumentCollectionConfigurationResponse(
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
