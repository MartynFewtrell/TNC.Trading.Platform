namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

/// <summary>Updates platform settings and optional environment-scoped market-category instrument collection limits.</summary>
internal sealed record UpdatePlatformConfigurationRequest(
    string BrokerEnvironment,
    UpdateTradingScheduleRequest TradingSchedule,
    UpdateRetryPolicyRequest RetryPolicy,
    UpdateNotificationSettingsRequest NotificationSettings,
    UpdateIgCredentialsRequest Credentials,
    string ChangedBy,
    int? InstrumentUpdatesPerDay = null,
    int? ApprovedNonTradingDailyRequestAllowance = null);
