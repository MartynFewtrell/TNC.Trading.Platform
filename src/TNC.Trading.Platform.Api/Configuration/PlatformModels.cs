using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Api.Configuration;

internal enum PlatformEnvironmentKind
{
    Test,
    Live
}

internal enum BrokerEnvironmentKind
{
    Demo,
    Live
}

internal enum WeekendBehavior
{
    ExcludeWeekends,
    IncludeSaturday,
    IncludeSunday,
    IncludeFullWeekend
}

internal enum PlatformSessionStatus
{
    Unknown,
    Active,
    Degraded,
    OutOfSchedule,
    Blocked
}

internal enum AuthRetryPhase
{
    None,
    InitialAutomatic,
    Periodic
}

internal sealed record TradingScheduleConfiguration(
    TimeOnly StartOfDay,
    TimeOnly EndOfDay,
    IReadOnlyList<DayOfWeek> TradingDays,
    WeekendBehavior WeekendBehavior,
    IReadOnlyList<DateOnly> BankHolidayExclusions,
    string TimeZone);

internal sealed record RetryPolicyConfiguration(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);

internal sealed record NotificationSettingsConfiguration(
    string Provider,
    string? EmailTo);

internal sealed record CredentialPresence(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword)
{
    [JsonIgnore]
    public bool IsComplete => HasApiKey && HasIdentifier && HasPassword;
}

internal sealed record PlatformConfigurationSnapshot(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings,
    CredentialPresence Credentials,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    DateTimeOffset UpdatedAtUtc,
    bool RestartRequired);

internal sealed record TradingScheduleStatus(
    bool IsActive,
    string Reason);

internal sealed record PlatformRetryState(
    AuthRetryPhase Phase,
    int AutomaticAttemptNumber,
    DateTimeOffset? NextRetryAtUtc,
    bool RetryLimitReached,
    bool ManualRetryAvailable);

internal sealed record PlatformStatusModel(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    bool LiveOptionVisible,
    bool LiveOptionAvailable,
    TradingScheduleConfiguration TradingSchedule,
    TradingScheduleStatus TradingScheduleStatus,
    PlatformSessionStatus SessionStatus,
    bool IsDegraded,
    string? BlockedReason,
    PlatformRetryState RetryState,
    DateTimeOffset UpdatedAtUtc);

internal sealed record OperationalEventModel(
    long EventId,
    string Category,
    string EventType,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string Summary,
    string Details,
    DateTimeOffset OccurredAtUtc);
