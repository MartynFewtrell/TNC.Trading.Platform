namespace TNC.Trading.Platform.Application.Features.MarketCategories;

/// <summary>Represents the complete saved catalogue and its last successful refresh time.</summary>
internal sealed record MarketCategorySnapshot(
    IReadOnlyList<MarketCategory> Categories,
    DateTimeOffset? LastRefreshedAtUtc);
