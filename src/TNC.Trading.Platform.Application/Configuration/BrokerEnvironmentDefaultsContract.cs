namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record BrokerEnvironmentDefaultsContract(
    Guid BrokerEnvironmentDefaultsId,
    int Version,
    bool IsActive,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings);