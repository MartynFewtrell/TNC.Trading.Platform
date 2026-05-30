namespace TNC.Trading.Platform.Web;

internal sealed record IgProofDataViewModel(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount,
    DateTimeOffset RetrievedAtUtc);
