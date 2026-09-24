using TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

/// <summary>Updated platform configuration, including safe applied-environment collection settings.</summary>
internal sealed record UpdatePlatformConfigurationResponse(
    string PlatformEnvironment,
    string BrokerEnvironment,
    UpdatedTradingScheduleResponse TradingSchedule,
    UpdatedRetryPolicyResponse RetryPolicy,
    UpdatedNotificationSettingsResponse NotificationSettings,
    UpdatedCredentialPresenceResponse Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc,
    InstrumentCollectionConfigurationResponse InstrumentCollection);
