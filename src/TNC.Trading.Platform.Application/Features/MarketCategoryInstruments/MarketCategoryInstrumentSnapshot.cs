namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>The last complete saved instrument set and its immutable collection provenance.</summary>
internal sealed record MarketCategoryInstrumentSnapshot(
    long SnapshotVersion,
    MarketCategoryInstrumentRunProvenance Provenance,
    IReadOnlyList<MarketCategoryInstrument> Instruments);
