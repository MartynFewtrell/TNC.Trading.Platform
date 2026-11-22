namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Safe pagination metadata for one requested category-instrument page.</summary>
internal sealed record MarketCategoryInstrumentPageEvidenceResponse(
    int RequestOrdinal,
    int RequestedPageNumber,
    int RequestedPageSize,
    int? ReturnedPageNumber,
    int? ReturnedPageSize,
    int? TotalPages,
    int? TotalResults,
    int RawCount,
    int? ExpectedCount);
