namespace TNC.Trading.Platform.Application.Configuration;

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
