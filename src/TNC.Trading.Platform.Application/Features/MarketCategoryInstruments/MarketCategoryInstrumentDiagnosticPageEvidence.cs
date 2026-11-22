namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Count-only evidence for one provider page; no instrument identity or payload is retained.</summary>
internal sealed record MarketCategoryInstrumentDiagnosticPageEvidence(
    int RequestOrdinal,
    int RequestedPageNumber,
    int RequestedPageSize,
    int? ReturnedPageNumber,
    int? ReturnedPageSize,
    int? TotalPages,
    int? TotalResults,
    DiagnosticJsonValueKind MetadataValueKind,
    DiagnosticJsonValueKind InstrumentsValueKind,
    DiagnosticJsonValueKind PageNumberValueKind,
    DiagnosticJsonValueKind PageSizeValueKind,
    DiagnosticJsonValueKind TotalPagesValueKind,
    DiagnosticJsonValueKind TotalResultsValueKind,
    int? RawArrayCount,
    int? DeserializedRowCount,
    int NullRowCount,
    int ValidRowCount,
    int InvalidRowCount,
    int DuplicateEpicCount,
    int DuplicateNameCount,
    int CrossPageEpicOverlapCount,
    int? FormulaExpectedRowCount,
    IReadOnlyList<string> DiscrepancyCodes);
