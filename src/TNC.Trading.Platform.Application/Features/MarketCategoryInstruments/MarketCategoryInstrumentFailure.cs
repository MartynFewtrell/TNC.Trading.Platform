namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A redacted failure classification without provider diagnostics or response content.</summary>
internal sealed record MarketCategoryInstrumentFailure(
    MarketCategoryInstrumentFailureCategory Category,
    bool IsRetryable,
    string? Reason = null,
    int? PageNumber = null,
    int? RowIndex = null,
    string? FieldName = null,
    string? Operation = null,
    int? HttpStatusCode = null,
    int? Expected = null,
    int? Actual = null);
