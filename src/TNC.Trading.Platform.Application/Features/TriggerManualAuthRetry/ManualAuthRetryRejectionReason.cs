namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

/// <summary>Identifies an expected reason a manual authentication retry cannot start.</summary>
public enum ManualAuthRetryRejectionReason
{
    ScheduleInactive,
    BlockedLive,
    RetryLimitNotReached
}