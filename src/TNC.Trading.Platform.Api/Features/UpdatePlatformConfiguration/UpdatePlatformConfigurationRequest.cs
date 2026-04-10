namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatePlatformConfigurationRequest(
    string PlatformEnvironment,
    string BrokerEnvironment,
    UpdateTradingScheduleRequest TradingSchedule,
    UpdateRetryPolicyRequest RetryPolicy,
    UpdateNotificationSettingsRequest NotificationSettings,
    UpdateIgCredentialsRequest Credentials,
    string ChangedBy);
