namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformStatusModel(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    TradingScheduleConfiguration TradingSchedule,
    TradingScheduleStatus TradingScheduleStatus,
    PlatformSessionStatus SessionStatus,
    bool IsDegraded,
    string? BlockedReason,
    PlatformRetryState RetryState,
    DateTimeOffset UpdatedAtUtc);
