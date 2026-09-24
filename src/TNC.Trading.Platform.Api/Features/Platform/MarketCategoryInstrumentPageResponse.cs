namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>One bounded page of a saved category instrument snapshot.</summary>
internal sealed record MarketCategoryInstrumentPageResponse(
    string State,
    string CategoryCode,
    long? SnapshotVersion,
    DateTimeOffset? LastRetrievedAtUtc,
    IReadOnlyList<MarketCategoryInstrumentResponse> Instruments,
    string? NextCursor);
