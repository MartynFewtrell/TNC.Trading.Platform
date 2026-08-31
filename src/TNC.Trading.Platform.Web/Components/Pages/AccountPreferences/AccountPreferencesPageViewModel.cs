namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class AccountPreferencesPageViewModel
{
    public bool TrailingStopsEnabled { get; set; }
    public string? AccountId { get; private set; }
    public bool? DesiredTrailingStopsEnabled { get; private set; }
    public long? DesiredRevision { get; private set; }
    public bool? ObservedTrailingStopsEnabled { get; private set; }
    public string? ObservedAccountId { get; private set; }
    public DateTimeOffset? DesiredChangedAtUtc { get; private set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; private set; }
    public DateTimeOffset? NextRetryAtUtc { get; private set; }
    public string? FailureSummary { get; private set; }
    public string VerificationStatus { get; private set; } = "Unconfigured";
    public bool? ConfirmedTrailingStopsEnabled { get; private set; }
    public bool HasConfirmedValue => DesiredTrailingStopsEnabled.HasValue;
    public bool HasPendingChange => ConfirmedTrailingStopsEnabled is bool confirmed && TrailingStopsEnabled != confirmed;
    public string ApplicationStatus { get; private set; } = string.Empty;
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public IReadOnlyList<AccountPreferencesObservationViewModel> History { get; private set; } = [];
    public string? NextCursor { get; private set; }

    public void Apply(AccountPreferencesViewModel preferences)
    {
        AccountId = preferences.AccountId;
        DesiredTrailingStopsEnabled = preferences.DesiredTrailingStopsEnabled;
        TrailingStopsEnabled = preferences.DesiredTrailingStopsEnabled ?? false;
        ConfirmedTrailingStopsEnabled = preferences.ObservedTrailingStopsEnabled;
        ObservedTrailingStopsEnabled = preferences.ObservedTrailingStopsEnabled;
        ObservedAccountId = preferences.ObservedAccountId;
        DesiredRevision = preferences.DesiredRevision;
        DesiredChangedAtUtc = preferences.DesiredChangedAtUtc;
        LastVerifiedAtUtc = preferences.LastVerifiedAtUtc;
        NextRetryAtUtc = preferences.NextRetryAtUtc;
        FailureSummary = preferences.FailureSummary;
        VerificationStatus = preferences.VerificationStatus;
        ApplicationStatus = preferences.ApplicationStatus;
        ObservedAtUtc = preferences.ObservedAtUtc ?? default;
    }

    public void Select(bool trailingStopsEnabled) => TrailingStopsEnabled = trailingStopsEnabled;

    public void ApplyHistory(AccountPreferencesHistoryViewModel history)
    {
        History = history.Observations;
        NextCursor = history.NextCursor;
    }
}
