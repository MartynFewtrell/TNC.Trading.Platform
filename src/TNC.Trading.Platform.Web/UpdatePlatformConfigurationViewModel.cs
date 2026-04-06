namespace TNC.Trading.Platform.Web;

internal sealed class UpdatePlatformConfigurationViewModel
{
    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public UpdateTradingScheduleViewModel TradingSchedule { get; set; } = new();

    public UpdateRetryPolicyViewModel RetryPolicy { get; set; } = new();

    public UpdateNotificationSettingsViewModel NotificationSettings { get; set; } = new();

    public UpdateCredentialsViewModel Credentials { get; set; } = new();

    public string ChangedBy { get; set; } = string.Empty;
}
