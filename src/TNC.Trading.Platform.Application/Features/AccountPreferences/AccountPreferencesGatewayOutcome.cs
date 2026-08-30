namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal abstract record AccountPreferencesGatewayOutcome
{
    private AccountPreferencesGatewayOutcome() { }

    internal sealed record Succeeded(AccountPreferences Preferences) : AccountPreferencesGatewayOutcome;
    internal sealed record Failed(AccountPreferencesFailureCategory Category, string SafeReason) : AccountPreferencesGatewayOutcome;
    internal sealed record Indeterminate(AccountPreferencesFailureCategory Category, string SafeReason) : AccountPreferencesGatewayOutcome;
    internal sealed record NotApplied(bool RequestedTrailingStopsEnabled, AccountPreferences ObservedPreferences) : AccountPreferencesGatewayOutcome;
}