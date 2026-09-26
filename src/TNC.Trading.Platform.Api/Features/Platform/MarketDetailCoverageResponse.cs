namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Independent persisted coverage for the selected market-detail category source.</summary>
internal sealed record MarketDetailCoverageResponse(
    bool IsFollowed,
    string State,
    int? ExpectedCount,
    int? CompletedCount,
    int? ExcludedCount,
    int? OutstandingCount,
    DateTimeOffset? LastCompleteAtUtc,
    DateTimeOffset? NextScheduledCheckUtc,
    string? SafeFailureCode);
