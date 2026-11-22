namespace TNC.Trading.Platform.Application.Features.Watchlists;

internal interface IWatchlistsGateway
{
    Task<WatchlistGatewayOutcome<IReadOnlyList<Watchlist>>> ListAsync(
        string expectedAccountId,
        CancellationToken cancellationToken);

    Task<WatchlistGatewayOutcome<IReadOnlyList<WatchlistMarket>>> GetMarketsAsync(
        string expectedAccountId,
        string watchlistId,
        CancellationToken cancellationToken);

    Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> CreateAsync(
        string expectedAccountId,
        string name,
        IReadOnlyList<string> epics,
        CancellationToken cancellationToken);

    Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> DeleteAsync(
        string expectedAccountId,
        string watchlistId,
        CancellationToken cancellationToken);

    Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> AddMarketAsync(
        string expectedAccountId,
        string watchlistId,
        string epic,
        CancellationToken cancellationToken);

    Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> RemoveMarketAsync(
        string expectedAccountId,
        string watchlistId,
        string epic,
        CancellationToken cancellationToken);
}
