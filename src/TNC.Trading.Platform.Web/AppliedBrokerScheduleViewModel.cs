namespace TNC.Trading.Platform.Web;

internal sealed record AppliedBrokerScheduleViewModel(
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
