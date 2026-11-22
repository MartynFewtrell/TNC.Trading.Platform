namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Sanitized request and pagination metadata for one category page response.</summary>
internal sealed record MarketCategoryInstrumentPageEvidence(
    int RequestOrdinal,
    int RequestedPageNumber,
    int RequestedPageSize,
    int? ReturnedPageNumber,
    int? ReturnedPageSize,
    int? TotalPages,
    int? TotalResults,
    int RawCount,
    int? ExpectedCount);
