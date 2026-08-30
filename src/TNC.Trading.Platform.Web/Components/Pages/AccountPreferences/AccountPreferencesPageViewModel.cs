namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class AccountPreferencesPageViewModel
{
    public bool TrailingStopsEnabled { get; set; }
    public string ApplicationStatus { get; private set; } = string.Empty;
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public IReadOnlyList<AccountPreferencesObservationViewModel> History { get; private set; } = [];
    public string? NextCursor { get; private set; }

    public void Apply(AccountPreferencesViewModel preferences)
    {
        TrailingStopsEnabled = preferences.TrailingStopsEnabled;
        ApplicationStatus = preferences.ApplicationStatus;
        ObservedAtUtc = preferences.ObservedAtUtc;
    }

    public void ApplyHistory(AccountPreferencesHistoryViewModel history)
    {
        History = history.Observations;
        NextCursor = history.NextCursor;
    }
}
