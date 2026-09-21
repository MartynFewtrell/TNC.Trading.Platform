namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal abstract record AccountPreferencesQueryState
{
    private AccountPreferencesQueryState() { }

    internal sealed record Unconfigured : AccountPreferencesQueryState;
    internal sealed record Configured(AccountPreferencesCurrentState State) : AccountPreferencesQueryState;
}