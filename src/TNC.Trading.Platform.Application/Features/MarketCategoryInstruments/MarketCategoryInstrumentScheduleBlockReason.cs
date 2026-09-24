namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal enum MarketCategoryInstrumentScheduleBlockReason
{
    ScheduleDisabled,
    UnsupportedAppliedEnvironment,
    InvalidSchedule,
    InvalidFrequency,
    ScheduleInactive,
    SlotAlreadyObserved
}
