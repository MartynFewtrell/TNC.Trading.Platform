namespace TNC.Trading.Platform.Application.Features.MarketCategories;

/// <summary>Safe, stable categories for failures returned by the provider boundary.</summary>
public enum MarketCategoriesFailureCategory
{
    UnsupportedEnvironment,
    Unauthorized,
    RateLimited,
    Unavailable,
    Timeout,
    MalformedProviderData,
    Rejected,
    Transient,
    AllowanceExceeded,
    ScheduleClosed
}
