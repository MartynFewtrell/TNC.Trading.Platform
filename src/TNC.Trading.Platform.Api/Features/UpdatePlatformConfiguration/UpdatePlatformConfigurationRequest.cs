namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatePlatformConfigurationRequest(
    string PlatformEnvironment,
    string BrokerEnvironment,
    UpdateTradingScheduleRequest TradingSchedule,
    UpdateRetryPolicyRequest RetryPolicy,
    UpdateNotificationSettingsRequest NotificationSettings,
    UpdateIgCredentialsRequest Credentials,
    string ChangedBy);

internal sealed record UpdateTradingScheduleRequest(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);

internal sealed record UpdateRetryPolicyRequest(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);

internal sealed record UpdateNotificationSettingsRequest(
    string Provider,
    string? EmailTo);

internal sealed record UpdateIgCredentialsRequest(
    string? ApiKey,
    string? Identifier,
    string? Password);
