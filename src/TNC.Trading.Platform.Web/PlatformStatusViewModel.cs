namespace TNC.Trading.Platform.Web;

internal sealed record PlatformStatusViewModel(
    string PlatformEnvironment,
    string BrokerEnvironment,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    TradingScheduleViewModel TradingSchedule,
    TradingScheduleStateViewModel TradingScheduleState,
    AuthStateViewModel AuthState,
    RetryStateViewModel RetryState,
    DateTimeOffset UpdatedAtUtc);
