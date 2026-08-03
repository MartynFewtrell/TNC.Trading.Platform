using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal sealed record PlatformConfigurationBootstrap(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings,
    string UpdatedBy);