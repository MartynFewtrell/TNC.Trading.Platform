using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Retrieves and validates every provider page for one category and applied environment.</summary>
internal interface IMarketCategoryInstrumentsGateway
{
    Task<MarketCategoryInstrumentCollectionResult> CollectCompleteAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        string categoryCode,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken);
}
