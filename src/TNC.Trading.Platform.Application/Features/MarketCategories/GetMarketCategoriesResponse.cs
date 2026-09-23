namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed record GetMarketCategoriesResponse(
    IReadOnlyList<MarketCategory> Categories,
    DateTimeOffset? LastRefreshedAtUtc,
    bool HasSavedSnapshot);
