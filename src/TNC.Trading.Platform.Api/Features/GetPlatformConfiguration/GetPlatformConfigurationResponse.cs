namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal sealed record GetPlatformConfigurationResponse(
    string PlatformEnvironment,
    string BrokerEnvironment,
    ConfigurationTradingScheduleResponse TradingSchedule,
    ConfigurationRetryPolicyResponse RetryPolicy,
    ConfigurationNotificationSettingsResponse NotificationSettings,
    CredentialPresenceResponse Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc);

internal sealed record ConfigurationTradingScheduleResponse(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);

internal sealed record ConfigurationRetryPolicyResponse(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);

internal sealed record ConfigurationNotificationSettingsResponse(
    string Provider,
    string? EmailTo);

internal sealed record CredentialPresenceResponse(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword);
