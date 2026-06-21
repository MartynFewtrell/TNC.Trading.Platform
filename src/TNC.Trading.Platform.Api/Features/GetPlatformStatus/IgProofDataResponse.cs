namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record IgProofDataResponse(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount,
    DateTimeOffset RetrievedAtUtc);
