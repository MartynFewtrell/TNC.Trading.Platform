namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A failure classification with bounded provider diagnostics and no response content.</summary>
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
    int? Actual = null,
    string? ProviderErrorCode = null,
    MarketCategoryProviderErrorCodeParseOutcome? ProviderErrorCodeParseOutcome = null,
    MarketCategoryInstrumentAuthenticationPhase? AuthenticationPhase = null,
    MarketCategoryInstrumentPageEvidence? InitialPageEvidence = null,
    MarketCategoryInstrumentPageEvidence? RestartedPageEvidence = null,
    int? HttpRequestCount = null);
