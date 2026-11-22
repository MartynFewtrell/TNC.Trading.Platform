namespace TNC.Trading.Platform.Application.Features.AccountHistory;

internal enum AccountHistoryFailureCategory
{
    UnsupportedEnvironment,
    Unauthorized,
    RateLimited,
    Unavailable,
    Timeout,
    MalformedProviderData,
    Rejected
}
