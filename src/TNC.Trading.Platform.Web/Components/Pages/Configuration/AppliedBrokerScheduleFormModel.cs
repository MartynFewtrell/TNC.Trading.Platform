using System.Globalization;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class AppliedBrokerScheduleFormModel
{
    public Guid BrokerEnvironmentId { get; set; }

    public int DefaultsVersion { get; set; }

    public bool LegacyReconciliationRequired { get; set; }

    public string StartOfDayText { get; set; } = "08:00";

    public string EndOfDayText { get; set; } = "17:00";

    public string TradingDaysCsv { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";

    public string WeekendBehavior { get; set; } = "ExcludeWeekends";

    public string BankHolidayCsv { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "Europe/London";

    public static AppliedBrokerScheduleFormModel From(AppliedBrokerScheduleViewModel schedule) => new()
    {
        BrokerEnvironmentId = schedule.BrokerEnvironmentId,
        DefaultsVersion = schedule.DefaultsVersion,
        LegacyReconciliationRequired = schedule.LegacyReconciliationRequired,
        StartOfDayText = schedule.StartOfDay.ToString("HH:mm", CultureInfo.InvariantCulture),
        EndOfDayText = schedule.EndOfDay.ToString("HH:mm", CultureInfo.InvariantCulture),
        TradingDaysCsv = string.Join(',', schedule.TradingDays),
        WeekendBehavior = schedule.WeekendBehavior,
        BankHolidayCsv = string.Join(',', schedule.BankHolidayExclusions.Select(date =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
        TimeZone = schedule.TimeZone
    };

    public UpdateAppliedBrokerScheduleViewModel ToRequest()
    {
        if (!TimeOnly.TryParseExact(StartOfDayText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startOfDay))
        {
            throw new FormatException("Enter the Trading Day start using 24-hour HH:mm time.");
        }

        if (!TimeOnly.TryParseExact(EndOfDayText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endOfDay))
        {
            throw new FormatException("Enter the Trading Day end using 24-hour HH:mm time.");
        }

        var tradingDays = TradingDaysCsv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(day => Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new FormatException($"'{day}' is not a valid weekday name."))
            .ToArray();
        var bankHolidayExclusions = BankHolidayCsv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(date => DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : throw new FormatException($"'{date}' is not a valid date. Use yyyy-MM-dd."))
            .ToArray();

        return new(startOfDay, endOfDay, tradingDays, WeekendBehavior, bankHolidayExclusions, TimeZone);
    }
}
