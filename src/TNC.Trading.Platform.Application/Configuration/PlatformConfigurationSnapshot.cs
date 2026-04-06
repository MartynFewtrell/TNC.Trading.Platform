namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformConfigurationSnapshot(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings,
    CredentialPresence Credentials,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    DateTimeOffset UpdatedAtUtc,
    bool RestartRequired);
