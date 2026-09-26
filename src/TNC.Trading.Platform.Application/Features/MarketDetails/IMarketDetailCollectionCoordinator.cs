namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailCollectionCoordinator
{
    Task<CollectMarketDetailsResponse> ExecuteDueCollectionAsync(CancellationToken cancellationToken);
}
