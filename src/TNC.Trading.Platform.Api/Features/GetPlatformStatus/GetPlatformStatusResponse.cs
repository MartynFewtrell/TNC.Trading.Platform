namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record GetPlatformStatusResponse(
    string PlatformEnvironment,
    string BrokerEnvironment,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    TradingScheduleResponse TradingSchedule,
    TradingScheduleStateResponse TradingScheduleState,
    AuthStateResponse AuthState,
    RetryStateResponse RetryState,
    DateTimeOffset UpdatedAtUtc);

internal sealed record TradingScheduleResponse(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);

internal sealed record TradingScheduleStateResponse(
    bool IsActive,
    string Reason);

internal sealed record AuthStateResponse(
    string SessionStatus,
    bool IsDegraded,
    string? BlockedReason);

internal sealed record RetryStateResponse(
    string Phase,
    int AutomaticAttemptNumber,
    DateTimeOffset? NextRetryAtUtc,
    bool RetryLimitReached,
    bool ManualRetryAvailable);
