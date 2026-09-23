namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed class RefreshMarketCategoriesHandler(
    IMarketCategoriesGateway gateway,
    IMarketCategorySnapshotStore snapshotStore,
    TimeProvider timeProvider)
{
    public async Task<RefreshMarketCategoriesResponse> HandleAsync(
        RefreshMarketCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await gateway.GetAsync(cancellationToken).ConfigureAwait(false);
        if (result is MarketCategoriesGatewayResult.Failed failed)
        {
            return new(new MarketCategoriesRefreshOutcome.Failed(failed.Category, failed.SafeReason));
        }

        var succeeded = (MarketCategoriesGatewayResult.Succeeded)result;
        var snapshot = new MarketCategorySnapshot(succeeded.Categories, timeProvider.GetUtcNow());
        var saved = await snapshotStore.ReplaceAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return new(new MarketCategoriesRefreshOutcome.Saved(saved));
    }
}
