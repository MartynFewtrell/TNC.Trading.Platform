namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal enum MarketDetailTargetFailureKind
{
    ProviderConfirmedUnavailable,
    TransientProviderFailure,
    RateLimited,
    AllowanceUnavailable,
    Unauthorized,
    InvalidResponse
}
