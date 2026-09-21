namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

public enum AccountPreferencesFailureCategory
{
    UnsupportedEnvironment,
    Unauthorized,
    RateLimited,
    MalformedProviderData,
    Rejected,
    Unavailable,
    Timeout,
    Unsupported,
    AccountMismatch,
    Transient
}