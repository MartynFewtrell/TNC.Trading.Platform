namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>A validated saved provider observation with retrieval provenance and separately typed sections.</summary>
internal sealed record MarketDetailObservationResponse(
    DateTimeOffset RetrievedAtUtc,
    string Source,
    string SourceEndpoint,
    int SourceVersion,
    string? ProviderUpdateTimeText,
    MarketDetailInstrumentResponse Instrument,
    MarketDetailDealingRulesResponse DealingRules,
    MarketDetailMarketSnapshotResponse Snapshot);
