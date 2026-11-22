namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Describes the bounded diagnostic traversal without implying collection completeness.</summary>
internal enum MarketCategoryInstrumentDiagnosticOutcome
{
    AdvertisedSequenceExhausted,
    InvalidRequest,
    UnsupportedEnvironment,
    MissingCredentials,
    InvalidPagingMetadata,
    PagingMetadataChanged,
    DiagnosticLimitReached,
    CallCeilingExceeded,
    RequestAllowanceInsufficient,
    ExecutionInvalidated,
    Cancelled,
    ResponseMalformed,
    ProviderRequestFailed
}
