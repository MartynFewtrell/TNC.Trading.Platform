namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailRunMembership(
    string CategoryCode,
    Guid ListingCollectionId,
    long ListingVersion);
