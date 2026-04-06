namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformConfigurationUpdate(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings,
    string? ApiKey,
    string? Identifier,
    string? Password,
    string ChangedBy);
