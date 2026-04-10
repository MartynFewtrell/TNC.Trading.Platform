namespace TNC.Trading.Platform.Infrastructure.Persistence;

internal sealed class AuthRuntimeStateEntity
{
    public int AuthRuntimeStateId { get; set; }

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string TradingScheduleStatus { get; set; } = string.Empty;

    public string SessionStatus { get; set; } = string.Empty;

    public bool IsDegraded { get; set; }

    public string? BlockedReason { get; set; }

    public string RetryPhase { get; set; } = string.Empty;

    public int AutomaticAttemptNumber { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public bool RetryLimitReached { get; set; }

    public Guid? CurrentRetryCycleId { get; set; }

    public DateTimeOffset? EstablishedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    public DateTimeOffset? LastTransitionAtUtc { get; set; }
}
