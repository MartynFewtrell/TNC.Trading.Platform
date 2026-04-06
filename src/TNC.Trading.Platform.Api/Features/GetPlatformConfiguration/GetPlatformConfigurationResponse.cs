namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal sealed record GetPlatformConfigurationResponse(
    string PlatformEnvironment,
    string BrokerEnvironment,
    ConfigurationTradingScheduleResponse TradingSchedule,
    ConfigurationRetryPolicyResponse RetryPolicy,
    ConfigurationNotificationSettingsResponse NotificationSettings,
    CredentialPresenceResponse Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc);
