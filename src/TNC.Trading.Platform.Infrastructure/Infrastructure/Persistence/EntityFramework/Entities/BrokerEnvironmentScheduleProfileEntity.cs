namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentScheduleProfileEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public int DefaultsVersion { get; set; }
    public TimeOnly TradingHoursStart { get; set; }
    public TimeOnly TradingHoursEnd { get; set; }
    public string TradingDaysCsv { get; set; } = string.Empty;
    public string WeekendBehavior { get; set; } = string.Empty;
    public string BankHolidayExclusionsJson { get; set; } = "[]";
    public string TimeZone { get; set; } = "UTC";
}