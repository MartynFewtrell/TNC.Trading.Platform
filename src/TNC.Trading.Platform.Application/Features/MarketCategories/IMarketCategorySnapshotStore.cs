using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal interface IMarketCategorySnapshotStore
{
    Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken);
    Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken);

    Task<MarketCategorySnapshot> ReplaceScheduledAsync(
        MarketCategorySnapshot snapshot,
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken cancellationToken) =>
        ReplaceAsync(snapshot, cancellationToken);

    Task<MarketCategorySnapshot> ReplaceManualScheduledAsync(
        MarketCategorySnapshot snapshot,
        BrokerEnvironmentKind appliedBrokerEnvironment,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Manual category refresh requires a schedule-guarded snapshot implementation.");
}
