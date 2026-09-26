namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal interface IMarketDetailsGateway
{
    Task<IReadOnlyList<MarketDetailGatewayResult>> GetMarketsAsync(
        MarketDetailGatewayRequest request,
        CancellationToken cancellationToken);
}
