namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailCategoryCoverage(
    string CategoryCode,
    bool IsFollowed,
    MarketDetailRunStatus AggregateStatus,
    MarketDetailRunCounts? Counts,
    DateTimeOffset? LastCompleteAtUtc,
    DateTimeOffset? NextScheduledCheckUtc,
    string? SafeFailureCode);
