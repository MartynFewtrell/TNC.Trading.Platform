namespace TNC.Trading.Platform.Web;

internal sealed record MarketCategoryInstrumentPageViewModel(
    string State,
    string CategoryCode,
    long? SnapshotVersion,
    DateTimeOffset? LastRetrievedAtUtc,
    IReadOnlyList<MarketCategoryInstrumentViewModel> Instruments,
    string? NextCursor);
