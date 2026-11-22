namespace TNC.Trading.Platform.Application.Features.Trading;

internal enum TradingFailureCategory
{
    UnsupportedEnvironment,
    Unauthorized,
    RateLimited,
    Unavailable,
    Timeout,
    MalformedProviderData,
    Rejected
}
