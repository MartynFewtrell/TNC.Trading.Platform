namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class ConfigurationFormModel
{
    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public UpdateTradingScheduleViewModel TradingSchedule { get; set; } = new()
    {
        StartOfDay = new TimeOnly(8, 0),
        EndOfDay = new TimeOnly(16, 30),
        WeekendBehavior = "ExcludeWeekends",
        TimeZone = "UTC"
    };

    public UpdateRetryPolicyViewModel RetryPolicy { get; set; } = new()
    {
        InitialDelaySeconds = 1,
        MaxAutomaticRetries = 5,
        Multiplier = 2,
        MaxDelaySeconds = 60,
        PeriodicDelayMinutes = 5
    };

    public UpdateNotificationSettingsViewModel NotificationSettings { get; set; } = new()
    {
        Provider = "RecordedOnly"
    };

    public CredentialPresenceViewModel Credentials { get; set; } = new(false, false, false, false, false, false, false, false);

    public UpdateCredentialsViewModel CredentialsUpdate { get; set; } = new();

    public bool RestartRequired { get; set; }

    public string ChangedBy { get; set; } = "operator";

    public int? InstrumentUpdatesPerDay { get; set; }

    public int? ApprovedNonTradingDailyRequestAllowance { get; set; }

    public int? MarketDataLeadInMinutes { get; set; }

    public InstrumentCollectionConfigurationViewModel InstrumentCollection { get; set; } =
        new(false, "SettingsUnavailable", null, null, null, null, null, null, null, null, null, null);
}