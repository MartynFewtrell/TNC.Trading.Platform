namespace TNC.Trading.Platform.Application.Features.Watchlists;

internal sealed record Watchlist(
    string Id,
    string Name,
    bool Editable,
    bool Deletable,
    bool IsDefaultSystemWatchlist);
