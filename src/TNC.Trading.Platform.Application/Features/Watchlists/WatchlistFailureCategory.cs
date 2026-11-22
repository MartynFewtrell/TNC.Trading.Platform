namespace TNC.Trading.Platform.Application.Features.Watchlists;

internal enum WatchlistFailureCategory
{
    UnsupportedEnvironment,
    Unauthorized,
    RateLimited,
    Unavailable,
    Timeout,
    MalformedProviderData,
    Rejected
}
