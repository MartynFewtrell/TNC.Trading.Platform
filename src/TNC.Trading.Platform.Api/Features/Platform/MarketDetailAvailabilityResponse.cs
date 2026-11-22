namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Compact saved market-detail availability for one listed instrument.</summary>
internal sealed record MarketDetailAvailabilityResponse(
    string State,
    DateTimeOffset? DetailRetrievedAtUtc,
    string? SafeFailureCode,
    string? FailureKind = null,
    string? ProviderErrorCode = null,
    int? HttpStatus = null,
    string? Operation = null);
