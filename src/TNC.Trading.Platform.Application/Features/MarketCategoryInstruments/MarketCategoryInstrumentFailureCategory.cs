namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal enum MarketCategoryInstrumentFailureCategory
{
    CategoryPrerequisiteFailed,
    Unavailable,
    IncompleteCollection,
    InvalidCollection,
    RateLimited,
    AllowanceUnavailable,
    AllowanceExceeded,
    ScheduleClosed,
    UnsupportedEnvironment,
    LeaseLost,
    StorageUnavailable,
    Cancelled,
    Timeout,
    Unauthorized,
    Rejected
}
