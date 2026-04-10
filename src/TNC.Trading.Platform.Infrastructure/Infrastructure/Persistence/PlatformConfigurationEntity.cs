namespace TNC.Trading.Platform.Infrastructure.Persistence;

internal sealed class PlatformConfigurationEntity
{
    public int ConfigurationId { get; set; }

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

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

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public bool RestartRequired { get; set; }
}
