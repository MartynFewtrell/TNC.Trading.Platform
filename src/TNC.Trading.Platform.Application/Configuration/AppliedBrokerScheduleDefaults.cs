namespace TNC.Trading.Platform.Application.Configuration;

internal static class AppliedBrokerScheduleDefaults
{
    public static TradingScheduleConfiguration Create() => new(
        new TimeOnly(8, 0),
        new TimeOnly(17, 0),
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        WeekendBehavior.ExcludeWeekends,
        [],
        "Europe/London");
}
