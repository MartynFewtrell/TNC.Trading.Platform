namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Atomically replaces the current snapshot only after a complete collection is validated.</summary>
internal interface IMarketCategoryInstrumentSnapshotWriter
{
    Task<MarketCategoryInstrumentSnapshot> SaveCompleteAsync(
        MarketCategoryInstrumentCollection collection,
        MarketCategoryInstrumentRunProvenance provenance,
        CancellationToken cancellationToken);
}
