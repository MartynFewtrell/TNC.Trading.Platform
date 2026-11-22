namespace TNC.Trading.Platform.Application.Features.AccountHistory;

internal abstract record AccountHistoryGatewayOutcome<T>
{
    private AccountHistoryGatewayOutcome()
    {
    }

    internal sealed record Succeeded(T Value, DateTimeOffset RetrievedAtUtc) : AccountHistoryGatewayOutcome<T>;

    internal sealed record Failed(AccountHistoryFailureCategory Category, string SafeReason) : AccountHistoryGatewayOutcome<T>;
}
