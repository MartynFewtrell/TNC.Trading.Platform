namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

/// <summary>Defines the stable, non-identifying contract for an account-bound preferences mismatch.</summary>
public static class AccountPreferencesAccountMismatch
{
    public const AccountPreferencesFailureCategory Category = AccountPreferencesFailureCategory.AccountMismatch;
    public const string Type = "/problems/account-preferences/account-mismatch";
    public const string Title = "IG session account does not match the configured account.";
    public const string Detail = "The authenticated IG account has changed. Trailing-stops preferences remain bound to the previously configured account. Reauthenticate with the intended account, then check status. Contact an administrator to change the configured account.";
}