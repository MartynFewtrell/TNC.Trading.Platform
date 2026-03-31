using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Application.Configuration;

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

internal sealed record UpdatePlatformConfigurationResult(
    PlatformConfigurationSnapshot Snapshot,
    bool RestartRequired);

internal sealed record ManualRetryResult(Guid RetryCycleId);

internal sealed class PlatformRuntimeState
{
    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string TradingScheduleStatus { get; set; } = string.Empty;

    public PlatformSessionStatus SessionStatus { get; set; }

    public bool IsDegraded { get; set; }

    public string? BlockedReason { get; set; }

    public AuthRetryPhase RetryPhase { get; set; }

    public int AutomaticAttemptNumber { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public bool RetryLimitReached { get; set; }

    public Guid? CurrentRetryCycleId { get; set; }

    public DateTimeOffset? EstablishedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    public DateTimeOffset? LastTransitionAtUtc { get; set; }
}

internal sealed class PlatformRetryCycle
{
    public Guid RetryCycleId { get; set; }

    public string CycleType { get; set; } = string.Empty;

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public AuthRetryPhase RetryPhase { get; set; }

    public int AutomaticAttemptNumber { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public int? LastDelaySeconds { get; set; }

    public int PeriodicDelayMinutes { get; set; }

    public int MaxAutomaticRetries { get; set; }

    public bool RetryLimitReached { get; set; }

    public bool FailureNotificationSent { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record PlatformEventRecord(
    string Category,
    string EventType,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string Severity,
    string Summary,
    object Details,
    string CorrelationId,
    Guid? RetryCycleId,
    DateTimeOffset OccurredAtUtc);
