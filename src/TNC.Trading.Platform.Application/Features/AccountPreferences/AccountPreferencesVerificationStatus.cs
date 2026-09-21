namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal enum AccountPreferencesVerificationStatus
{
    Unconfigured,
    Pending,
    InSync,
    Drifted,
    VerificationFailed,
    Unsupported
}