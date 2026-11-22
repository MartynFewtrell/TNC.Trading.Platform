namespace TNC.Trading.Platform.Application.Features.MarketData;

internal enum MarketDataFailureCategory
{
    UnsupportedEnvironment,
    Unauthorized,
    RateLimited,
    Unavailable,
    Timeout,
    MalformedProviderData,
    Rejected
}
