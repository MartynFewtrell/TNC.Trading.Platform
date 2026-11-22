namespace TNC.Trading.Platform.Application.Features.MarketData;

internal interface IMarketDataGateway
{
    Task<MarketDataGatewayOutcome<MarketDetails>> GetMarketDetailsAsync(
        string epic,
        CancellationToken cancellationToken);

    Task<MarketDataGatewayOutcome<IReadOnlyList<MarketDetails>>> GetMarketDetailsAsync(
        IReadOnlyCollection<string> epics,
        CancellationToken cancellationToken);

    Task<MarketDataGatewayOutcome<IReadOnlyList<MarketListing>>> SearchMarketsAsync(
        string searchTerm,
        CancellationToken cancellationToken);

    Task<MarketDataGatewayOutcome<IReadOnlyList<MarketPriceBar>>> GetPricesByDateRangeAsync(
        string epic,
        string resolution,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken);

    Task<MarketDataGatewayOutcome<IReadOnlyList<MarketPriceBar>>> GetPricesByPointCountAsync(
        string epic,
        string resolution,
        int pointCount,
        CancellationToken cancellationToken);
}
