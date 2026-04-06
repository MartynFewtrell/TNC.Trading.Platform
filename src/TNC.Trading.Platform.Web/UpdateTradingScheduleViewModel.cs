namespace TNC.Trading.Platform.Web;

internal sealed class UpdateTradingScheduleViewModel
{
    public TimeOnly StartOfDay { get; set; }

    public TimeOnly EndOfDay { get; set; }

    public IReadOnlyList<DayOfWeek> TradingDays { get; set; } = [];

    public string WeekendBehavior { get; set; } = string.Empty;

    public IReadOnlyList<DateOnly> BankHolidayExclusions { get; set; } = [];

    public string TimeZone { get; set; } = string.Empty;
}
