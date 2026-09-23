namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal abstract record MarketCategoriesRefreshOutcome
{
    private MarketCategoriesRefreshOutcome() { }

    internal sealed record Saved(MarketCategorySnapshot Snapshot) : MarketCategoriesRefreshOutcome;
    internal sealed record Failed(MarketCategoriesFailureCategory Category, string SafeReason) : MarketCategoriesRefreshOutcome;
}
