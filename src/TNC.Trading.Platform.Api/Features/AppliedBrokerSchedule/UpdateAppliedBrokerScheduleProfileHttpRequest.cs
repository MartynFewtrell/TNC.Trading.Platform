namespace TNC.Trading.Platform.Api.Features.AppliedBrokerSchedule;

internal sealed record UpdateAppliedBrokerScheduleProfileHttpRequest(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);
