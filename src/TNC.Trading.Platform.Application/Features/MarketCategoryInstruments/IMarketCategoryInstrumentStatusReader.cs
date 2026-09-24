using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Reads durable collector progress and safe category outcomes from SQL only.</summary>
internal interface IMarketCategoryInstrumentStatusReader
{
    Task<MarketCategoryInstrumentCollectionStatus> ReadAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        DateOnly tradingDay,
        CancellationToken cancellationToken);
}
