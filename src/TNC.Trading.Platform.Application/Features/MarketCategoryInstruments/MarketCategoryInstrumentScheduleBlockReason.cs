namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal enum MarketCategoryInstrumentScheduleBlockReason
{
    ScheduleDisabled,
    UnsupportedAppliedEnvironment,
    LegacyScheduleReconciliationRequired,
    InvalidSchedule,
    InvalidFrequency,
    TimedUpdatesDisabled,
    ScheduleInactive,
    SlotAlreadyObserved
}
