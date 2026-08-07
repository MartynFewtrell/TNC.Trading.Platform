namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal enum AccountDetailsFailureCategory
{
    AllowanceLimited,
    Unavailable,
    MalformedProviderData,
    Timeout,
    Conflict,
    UnsupportedEnvironment
}
