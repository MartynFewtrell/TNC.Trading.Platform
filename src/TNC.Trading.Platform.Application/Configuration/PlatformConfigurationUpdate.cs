namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformConfigurationUpdate(
    BrokerEnvironmentKind BrokerEnvironment,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings,
    string? ApiKey,
    string? Identifier,
    string? Password,
    string ChangedBy,
    int? InstrumentUpdatesPerDay = null,
    int? ApprovedNonTradingDailyRequestAllowance = null);
