using TNC.Trading.Platform.Application.Features.MarketDataRuns;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailCollectionCoordinator
{
    Task<CollectMarketDetailsResponse> ExecuteDueCollectionAsync(
        CancellationToken cancellationToken,
        MarketDataFullRunLease? fullRunLease = null);
}
