namespace TNC.Trading.Platform.Api.Features.AppliedBrokerSchedule;

internal sealed record AppliedBrokerScheduleProfileResponse(
    Guid BrokerEnvironmentId,
    int DefaultsVersion,
    long ScheduleVersion,
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone,
    bool LegacyReconciliationRequired);
