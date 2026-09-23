namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed record MarketCategoriesViewModel(
    IReadOnlyList<MarketCategoryViewModel> Categories,
    DateTimeOffset? LastRefreshedAtUtc)
{
    public bool HasSavedSnapshot => LastRefreshedAtUtc is not null;
}

internal sealed record MarketCategoryViewModel(string Code, bool NonTradeable);
