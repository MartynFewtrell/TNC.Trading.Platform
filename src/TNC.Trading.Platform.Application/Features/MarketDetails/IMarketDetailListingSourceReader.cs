using TNC.Trading.Platform.Application.Features.MarketDataRuns;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailListingSourceReader
{
    Task<MarketDetailListingSourceSnapshot> ReadAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string appliedEndpointProfile,
        CancellationToken cancellationToken);

    Task<MarketDetailListingSourceSnapshot> ReadAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string appliedEndpointProfile,
        IReadOnlyList<string>? frozenCategoryCodes,
        CancellationToken cancellationToken) =>
        ReadAsync(key, scheduleRevision, appliedEndpointProfile, cancellationToken);
}
