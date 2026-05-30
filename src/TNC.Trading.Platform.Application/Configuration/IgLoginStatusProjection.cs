namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record IgLoginStatusProjection(
    string CurrentState,
    TradingScheduleStatus ScheduleState,
    PlatformRetryState RetryState,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessfulLoginAtUtc,
    Guid? LatestSnapshotId,
    string? LatestFailureSummary,
    IgLoginSnapshot? LatestSnapshot);
