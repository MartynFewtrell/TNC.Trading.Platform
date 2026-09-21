namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatePlatformConfigurationRequest(
    string BrokerEnvironment,
    UpdateTradingScheduleRequest TradingSchedule,
    UpdateRetryPolicyRequest RetryPolicy,
    UpdateNotificationSettingsRequest NotificationSettings,
    UpdateIgCredentialsRequest Credentials,
    string ChangedBy);
