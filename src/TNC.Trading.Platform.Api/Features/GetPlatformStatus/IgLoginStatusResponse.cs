namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record IgLoginStatusResponse(
    string CurrentState,
    TradingScheduleStateResponse ScheduleState,
    RetryStateResponse RetryState,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessfulLoginAtUtc,
    Guid? LatestSnapshotId,
    string? LatestFailureSummary,
    IgLoginSnapshotResponse? LatestSnapshot);
