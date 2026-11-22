namespace TNC.Trading.Platform.Web;

internal sealed record MarketCategoryPageEvidenceViewModel(
    int RequestOrdinal,
    int RequestedPageNumber,
    int RequestedPageSize,
    int? ReturnedPageNumber,
    int? ReturnedPageSize,
    int? TotalPages,
    int? TotalResults,
    int RawCount,
    int? ExpectedCount);
