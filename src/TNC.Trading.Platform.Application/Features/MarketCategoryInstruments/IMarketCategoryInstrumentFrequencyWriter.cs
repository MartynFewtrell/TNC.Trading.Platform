using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Persists a validated frequency change and its next-local-trading-day effective date.</summary>
internal interface IMarketCategoryInstrumentFrequencyWriter
{
    Task SaveAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        MarketCategoryInstrumentFrequency frequency,
        CancellationToken cancellationToken);
}
