namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatePlatformConfigurationResponse(
    string PlatformEnvironment,
    string BrokerEnvironment,
    UpdatedTradingScheduleResponse TradingSchedule,
    UpdatedRetryPolicyResponse RetryPolicy,
    UpdatedNotificationSettingsResponse NotificationSettings,
    UpdatedCredentialPresenceResponse Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc);
