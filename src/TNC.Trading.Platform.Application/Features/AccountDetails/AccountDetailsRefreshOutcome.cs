namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal abstract record AccountDetailsRefreshOutcome
{
    private AccountDetailsRefreshOutcome() { }

    internal sealed record Saved(AccountDetailsSnapshot Snapshot) : AccountDetailsRefreshOutcome;
    internal sealed record RefreshInProgress(DateTimeOffset? LatestRetrievedAtUtc) : AccountDetailsRefreshOutcome;
    internal sealed record Deferred : AccountDetailsRefreshOutcome;
    internal sealed record Failed(AccountDetailsFailureCategory Category, string Summary) : AccountDetailsRefreshOutcome;
}
