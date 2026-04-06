namespace TNC.Trading.Platform.Web;

internal sealed record PlatformConfigurationViewModel(
    string PlatformEnvironment,
    string BrokerEnvironment,
    TradingScheduleViewModel TradingSchedule,
    RetryPolicyViewModel RetryPolicy,
    NotificationSettingsViewModel NotificationSettings,
    CredentialPresenceViewModel Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc);
