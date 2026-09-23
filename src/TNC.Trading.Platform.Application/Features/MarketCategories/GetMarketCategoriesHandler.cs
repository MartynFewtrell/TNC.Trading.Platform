namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed class GetMarketCategoriesHandler(IMarketCategorySnapshotStore snapshotStore)
{
    public async Task<GetMarketCategoriesResponse> HandleAsync(
        GetMarketCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return new([], null, false);
        }

        var categories = snapshot.Categories
            .OrderBy(category => category.Code, StringComparer.Ordinal)
            .ToArray();
        return new(categories, snapshot.LastRefreshedAtUtc, true);
    }
}
