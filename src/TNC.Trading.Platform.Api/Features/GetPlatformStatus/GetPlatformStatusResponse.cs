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
