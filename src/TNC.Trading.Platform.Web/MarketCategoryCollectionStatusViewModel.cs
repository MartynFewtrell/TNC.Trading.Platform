namespace TNC.Trading.Platform.Web;

internal sealed record MarketCategoryCollectionStatusViewModel(
    string CategoryCode,
    DateTimeOffset? LastSuccessfulCollectionAtUtc,
    int Attempts,
    string? Outcome,
    string? SafeFailure,
    MarketDetailCoverageViewModel? DetailCoverage = null,
    DateTimeOffset? LastAttemptAtUtc = null,
    string? FailureKind = null,
    string? ProviderErrorCode = null,
    int? HttpStatus = null,
    string? Operation = null,
    int? PageNumber = null,
    int? Expected = null,
    int? Actual = null,
    string? ProviderErrorCodeParseOutcome = null,
    string? AuthenticationPhase = null,
    MarketCategoryPageEvidenceViewModel? InitialPageEvidence = null,
    MarketCategoryPageEvidenceViewModel? RestartedPageEvidence = null,
    int? HttpRequestCount = null);
