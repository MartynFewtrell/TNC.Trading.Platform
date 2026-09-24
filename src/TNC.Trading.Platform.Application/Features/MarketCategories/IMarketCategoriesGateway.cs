using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal interface IMarketCategoriesGateway
{
    Task<MarketCategoriesGatewayResult> GetAsync(CancellationToken cancellationToken);

    Task<MarketCategoriesGatewayResult> GetAsync(
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken) =>
        GetAsync(cancellationToken);
}

internal abstract record MarketCategoriesGatewayResult
{
    private MarketCategoriesGatewayResult() { }

    internal sealed record Succeeded(IReadOnlyList<MarketCategory> Categories) : MarketCategoriesGatewayResult;
    internal sealed record Failed(MarketCategoriesFailureCategory Category, string SafeReason) : MarketCategoriesGatewayResult;
}
