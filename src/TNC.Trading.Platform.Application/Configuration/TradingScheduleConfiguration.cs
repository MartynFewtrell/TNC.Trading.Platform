namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record TradingScheduleConfiguration(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    WeekendBehavior WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);
