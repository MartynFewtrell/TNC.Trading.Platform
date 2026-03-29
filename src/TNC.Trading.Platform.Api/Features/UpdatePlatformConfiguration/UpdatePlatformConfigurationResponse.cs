namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatePlatformConfigurationResponse(
    string PlatformEnvironment,
    string BrokerEnvironment,
    UpdatedTradingScheduleResponse TradingSchedule,
    UpdatedRetryPolicyResponse RetryPolicy,
    UpdatedNotificationSettingsResponse NotificationSettings,
    UpdatedCredentialPresenceResponse Credentials,
    bool RestartRequired,
    DateTimeOffset UpdatedAtUtc);

internal sealed record UpdatedTradingScheduleResponse(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    string WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);

internal sealed record UpdatedRetryPolicyResponse(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);

internal sealed record UpdatedNotificationSettingsResponse(
    string Provider,
    string? EmailTo);

internal sealed record UpdatedCredentialPresenceResponse(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword);
