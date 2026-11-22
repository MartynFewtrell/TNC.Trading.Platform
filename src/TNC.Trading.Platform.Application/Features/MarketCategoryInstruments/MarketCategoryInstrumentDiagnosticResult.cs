namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Sanitized evidence from a bounded, explicitly nonpublishing category diagnostic.</summary>
internal sealed record MarketCategoryInstrumentDiagnosticResult(
    MarketCategoryInstrumentDiagnosticOutcome Outcome,
    int PageSize,
    int MaximumPages,
    int MaximumResults,
    int MaximumRows,
    int MaximumHttpRequests,
    int? AdvertisedPageCount,
    int? AdvertisedResultCount,
    int HttpRequestCount,
    int PageRequestCount,
    int PageResultCount,
    int PagesVisited,
    int TotalRawRowCount,
    int TotalDeserializedRowCount,
    int NullRowCount,
    int ValidRowCount,
    int InvalidRowCount,
    int UniqueValidRowCount,
    int DuplicateEpicCount,
    int DuplicateNameCount,
    int CrossPageEpicOverlapCount,
    string? ReasonCode,
    IReadOnlyList<MarketCategoryInstrumentDiagnosticPageEvidence> Pages);
