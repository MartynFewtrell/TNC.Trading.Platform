namespace TNC.Trading.Platform.Web;

internal sealed record MarketDetailAvailabilityViewModel(
    string State,
    DateTimeOffset? DetailRetrievedAtUtc,
    string? SafeFailureCode,
    string? FailureKind = null,
    string? ProviderErrorCode = null,
    int? HttpStatus = null,
    string? Operation = null);
