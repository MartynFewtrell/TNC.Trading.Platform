namespace TNC.Trading.Platform.Application.Features.Watchlists;

internal sealed record WatchlistMutationAcknowledgement(
    string? CreatedWatchlistId,
    bool PartiallySucceeded);
