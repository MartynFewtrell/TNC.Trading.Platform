namespace TNC.Trading.Platform.Application.Configuration;

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
