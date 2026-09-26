namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailListingSourceReader
{
    Task<MarketDetailListingSourceSnapshot> ReadAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string appliedEndpointProfile,
        CancellationToken cancellationToken);
}
