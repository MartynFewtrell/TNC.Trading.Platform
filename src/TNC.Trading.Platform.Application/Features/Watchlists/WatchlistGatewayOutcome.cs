namespace TNC.Trading.Platform.Application.Features.Watchlists;

internal abstract record WatchlistGatewayOutcome<T>
{
    private WatchlistGatewayOutcome()
    {
    }

    internal sealed record Succeeded(T Value, DateTimeOffset RetrievedAtUtc) : WatchlistGatewayOutcome<T>;

    internal sealed record Failed(WatchlistFailureCategory Category, string SafeReason) : WatchlistGatewayOutcome<T>;

    internal sealed record Indeterminate(WatchlistFailureCategory Category, string SafeReason) : WatchlistGatewayOutcome<T>;
}
