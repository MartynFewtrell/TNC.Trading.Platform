namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Saved market-detail state for one current category/EPIC membership.</summary>
internal sealed record MarketDetailResponse(
    string State,
    string CategoryCode,
    string Epic,
    long? ListingSnapshotVersion,
    DateTimeOffset? ListingRetrievedAtUtc,
    MarketDetailCoverageResponse Coverage,
    MarketDetailObservationResponse? SavedObservation);
