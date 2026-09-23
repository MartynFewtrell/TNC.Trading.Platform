namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentDefaultsEntity
{
    public Guid BrokerEnvironmentDefaultsId { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public TimeOnly TradingHoursStart { get; set; }
    public TimeOnly TradingHoursEnd { get; set; }
    public string TradingDaysCsv { get; set; } = string.Empty;
    public string WeekendBehavior { get; set; } = string.Empty;
    public string BankHolidayExclusionsJson { get; set; } = "[]";
    public string TimeZone { get; set; } = "UTC";
    public int RetryInitialDelaySeconds { get; set; }
    public int RetryMaxAutomaticRetries { get; set; }
    public int RetryMultiplier { get; set; }
    public int RetryMaxDelaySeconds { get; set; }
    public int RetryPeriodicDelayMinutes { get; set; }
    public string NotificationProvider { get; set; } = string.Empty;
    public string? NotificationEmailTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}