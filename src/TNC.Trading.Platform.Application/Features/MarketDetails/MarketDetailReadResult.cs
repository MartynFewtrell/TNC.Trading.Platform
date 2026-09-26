namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailReadResult(
    bool CurrentMembershipExists,
    bool ListingVersionMatches,
    long? ListingSnapshotVersion,
    DateTimeOffset? ListingRetrievedAtUtc,
    bool IsCategoryFollowed,
    MarketDetailTargetStatus TargetStatus,
    MarketDetailValidatedObservation? Observation,
    MarketDetailRunStatus AggregateStatus,
    MarketDetailRunCounts? Counts,
    DateTimeOffset? LastCompleteAtUtc,
    DateTimeOffset? NextScheduledCheckUtc,
    string? SafeFailureCode,
    MarketDetailGatewayFailure? LatestFailure);
