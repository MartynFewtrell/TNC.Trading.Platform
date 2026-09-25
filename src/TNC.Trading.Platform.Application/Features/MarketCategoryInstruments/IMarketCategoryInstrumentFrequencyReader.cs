using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Reads environment-scoped effective collection frequency.</summary>
internal interface IMarketCategoryInstrumentFrequencyReader
{
    Task<MarketCategoryInstrumentFrequency> ReadAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        CancellationToken cancellationToken);
}
