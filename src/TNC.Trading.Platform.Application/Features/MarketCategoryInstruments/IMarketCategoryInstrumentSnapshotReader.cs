namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Reads saved instrument snapshots only; implementations must not contact the provider.</summary>
internal interface IMarketCategoryInstrumentSnapshotReader
{
    Task<MarketCategoryInstrumentSnapshotPage?> ReadPageAsync(
        MarketCategoryInstrumentSnapshotPageRequest request,
        CancellationToken cancellationToken);
}
