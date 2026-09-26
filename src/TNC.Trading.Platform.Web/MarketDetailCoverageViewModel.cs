namespace TNC.Trading.Platform.Web;

internal sealed record MarketDetailCoverageViewModel(
    bool IsFollowed,
    string State,
    int? ExpectedCount,
    int? CompletedCount,
    int? ExcludedCount,
    int? OutstandingCount,
    DateTimeOffset? LastCompleteAtUtc,
    DateTimeOffset? NextScheduledCheckUtc,
    string? SafeFailureCode);
