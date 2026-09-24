namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Platform-derived completeness and validation evidence attached to a successful run.</summary>
internal sealed record MarketCategoryInstrumentDataQualityEvidence(
    MarketCategoryInstrumentDataQualityStatus Status,
    int ValidatedInstrumentCount,
    int MissingOptionalValueCount);
