namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailAvailabilityReadResult(
    bool ListingSnapshotExists,
    bool ListingVersionMatches,
    long? ListingSnapshotVersion,
    DateTimeOffset? ListingRetrievedAtUtc,
    MarketDetailCategoryCoverage Coverage,
    IReadOnlyList<MarketDetailAvailability> Instruments);
