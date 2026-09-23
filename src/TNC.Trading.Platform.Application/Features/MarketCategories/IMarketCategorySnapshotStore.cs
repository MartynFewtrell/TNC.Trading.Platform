namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal interface IMarketCategorySnapshotStore
{
    Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken);
    Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken);
}
