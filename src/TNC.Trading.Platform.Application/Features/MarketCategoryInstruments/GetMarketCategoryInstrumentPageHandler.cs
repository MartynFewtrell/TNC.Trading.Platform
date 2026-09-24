using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class GetMarketCategoryInstrumentPageHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategorySnapshotStore categorySnapshotStore,
    IMarketCategoryInstrumentSnapshotReader snapshotReader)
{
    public async Task<GetMarketCategoryInstrumentPageResponse> HandleAsync(
        GetMarketCategoryInstrumentPageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CategoryCode)
            || request.CategoryCode.Length > 128
            || request.PageSize is < 1 or > 100
            || request.CursorSnapshotVersion is < 1
            || request.CursorAfterEpic is { Length: 0 or > 64 })
        {
            throw new ArgumentException("The category page request is invalid.", nameof(request));
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var environment))
        {
            return new(MarketCategoryInstrumentPageReadStatus.AppliedEnvironmentUnavailable, null, request.CategoryCode, null);
        }

        if (request.CursorSnapshotVersion is not null
            && (!string.Equals(request.CursorCategoryCode, request.CategoryCode, StringComparison.Ordinal)
                || !string.Equals(request.CursorBrokerEnvironment, environment.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return new(MarketCategoryInstrumentPageReadStatus.StaleCursor, environment, request.CategoryCode, null);
        }

        var categories = await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (categories is null || !categories.Categories.Any(item =>
                string.Equals(item.Code, request.CategoryCode, StringComparison.Ordinal)))
        {
            return new(MarketCategoryInstrumentPageReadStatus.CategoryNotFound, environment, request.CategoryCode, null);
        }

        var page = await snapshotReader.ReadPageAsync(
            new(
                environment,
                request.CategoryCode,
                request.CursorSnapshotVersion,
                request.CursorAfterEpic,
                request.PageSize),
            cancellationToken).ConfigureAwait(false);
        if (page is null)
        {
            return new(
                request.CursorSnapshotVersion is null
                    ? MarketCategoryInstrumentPageReadStatus.NeverCollected
                    : MarketCategoryInstrumentPageReadStatus.StaleCursor,
                environment,
                request.CategoryCode,
                null);
        }

        return new(MarketCategoryInstrumentPageReadStatus.Page, environment, request.CategoryCode, page);
    }
}
