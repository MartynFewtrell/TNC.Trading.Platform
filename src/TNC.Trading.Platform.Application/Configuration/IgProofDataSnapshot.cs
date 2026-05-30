namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record IgProofDataSnapshot(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount,
    DateTimeOffset RetrievedAtUtc);
