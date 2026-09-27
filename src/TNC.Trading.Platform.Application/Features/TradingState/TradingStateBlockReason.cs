namespace TNC.Trading.Platform.Application.Features.TradingState;

internal enum TradingStateBlockReason
{
    AppliedBrokerUnavailable,
    AppliedBrokerMismatch,
    InvalidTradingSchedule,
    TradingWindowClosed,
    TradingReadinessUnavailable,
    TestEnvironmentCannotTargetLiveBroker,
    MarketDataUnavailable,
    ScheduleReconciliationRequired,
    RequestAllowanceNotApproved,
    CollectionSettingsInvalid
}
