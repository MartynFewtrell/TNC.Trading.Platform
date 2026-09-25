namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A bounded page from one saved snapshot; the next key is an EPIC, not a provider cursor.</summary>
internal sealed record MarketCategoryInstrumentSnapshotPage(
    long SnapshotVersion,
    DateTimeOffset LastRetrievedAtUtc,
    IReadOnlyList<MarketCategoryInstrument> Instruments,
    string? NextEpic);
