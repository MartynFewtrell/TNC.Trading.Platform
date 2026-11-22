using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Current category snapshot time and the most recent slot's retry/failure outcome.</summary>
internal sealed record MarketCategoryInstrumentCategoryStatus(
    string CategoryCode,
    DateTimeOffset? LastSuccessfulCollectionAtUtc,
    int Attempts,
    string? Outcome,
    string? SafeFailure,
    MarketDetailCategoryCoverage? DetailCoverage = null,
    DateTimeOffset? LastAttemptAtUtc = null,
    MarketCategoryInstrumentFailureCategory? FailureKind = null,
    int? HttpStatus = null,
    string? Operation = null,
    int? PageNumber = null,
    int? Expected = null,
    int? Actual = null,
    string? ProviderErrorCode = null,
    MarketCategoryProviderErrorCodeParseOutcome? ProviderErrorCodeParseOutcome = null,
    MarketCategoryInstrumentAuthenticationPhase? AuthenticationPhase = null,
    MarketCategoryInstrumentPageEvidence? InitialPageEvidence = null,
    MarketCategoryInstrumentPageEvidence? RestartedPageEvidence = null,
    int? HttpRequestCount = null);
