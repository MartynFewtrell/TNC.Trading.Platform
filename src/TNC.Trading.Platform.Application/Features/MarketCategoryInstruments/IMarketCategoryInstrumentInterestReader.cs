using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Reads saved environment-scoped interest without invoking collection.</summary>
internal interface IMarketCategoryInstrumentInterestReader
{
    Task<MarketCategoryInstrumentInterestState> ReadAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        CancellationToken cancellationToken);
}
