namespace TNC.Trading.Platform.Web;

internal sealed record IgLoginStatusViewModel(
    string CurrentState,
    TradingScheduleStateViewModel ScheduleState,
    RetryStateViewModel RetryState,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessfulLoginAtUtc,
    Guid? LatestSnapshotId,
    string? LatestFailureSummary,
    IgLoginSnapshotViewModel? LatestSnapshot);
