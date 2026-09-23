namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal interface IMarketCategoriesGateway
{
    Task<MarketCategoriesGatewayResult> GetAsync(CancellationToken cancellationToken);
}

internal abstract record MarketCategoriesGatewayResult
{
    private MarketCategoriesGatewayResult() { }

    internal sealed record Succeeded(IReadOnlyList<MarketCategory> Categories) : MarketCategoriesGatewayResult;
    internal sealed record Failed(MarketCategoriesFailureCategory Category, string SafeReason) : MarketCategoriesGatewayResult;
}
