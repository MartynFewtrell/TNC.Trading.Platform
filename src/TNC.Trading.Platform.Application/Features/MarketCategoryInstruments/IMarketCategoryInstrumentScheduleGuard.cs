using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Revalidates the current applied environment and local schedule for each provider call.</summary>
internal interface IMarketCategoryInstrumentScheduleGuard
{
    Task<bool> IsStillActiveAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken);
}
