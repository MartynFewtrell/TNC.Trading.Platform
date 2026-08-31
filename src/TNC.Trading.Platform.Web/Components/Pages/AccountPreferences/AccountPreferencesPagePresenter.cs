namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class AccountPreferencesPagePresenter(PlatformApiClient platformApiClient)
{
    public AccountPreferencesPageViewModel State { get; } = new();
    public bool IsLoading { get; private set; }
    public bool IsSaving { get; private set; }
    public bool IsOperating { get; private set; }
    public string? CurrentError { get; private set; }
    public string? HistoryError { get; private set; }
    public string? Error => CurrentError;
    public string? SaveError { get; private set; }
    public string? Message { get; private set; }

    public void Select(bool trailingStopsEnabled)
    {
        State.Select(trailingStopsEnabled);
        SaveError = null;
        Message = null;
    }

    public void DismissConfirmation() => Message = null;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        CurrentError = null;
        HistoryError = null;
        try
        {
            State.Apply(await platformApiClient.GetAccountPreferencesAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            CurrentError = CreateErrorMessage("Unable to load account preferences", exception);
        }

        if (CurrentError is null)
        {
            try
            {
                await LoadHistoryAsync(null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                HistoryError = CreateErrorMessage("Unable to load account preferences history", exception);
            }
        }
        IsLoading = false;
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;
        SaveError = null;
        Message = null;
        try
        {
            State.Apply(await platformApiClient.UpdateAccountPreferencesAsync(State.TrailingStopsEnabled, cancellationToken));
            Message = "Trailing stops preference saved and confirmed.";
            try
            {
                await LoadHistoryAsync(null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                Message = "Trailing stops preference saved and confirmed. Observed history could not be refreshed.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            SaveError = "Unable to save and confirm account preferences.";
        }
        finally { IsSaving = false; }
    }

    public async Task LoadNextHistoryPageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadHistoryAsync(State.NextCursor, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            HistoryError = CreateErrorMessage("Unable to load account preferences history", exception);
        }
    }

    public async Task RetryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(State.AccountId)) return;
        IsOperating = true; CurrentError = null;
        try { State.Apply(await platformApiClient.RetryAccountPreferencesVerificationAsync(State.AccountId, cancellationToken)); }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException) { CurrentError = CreateErrorMessage("Unable to retry account preferences verification", exception); }
        finally { IsOperating = false; }
    }

    public async Task RemediateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(State.AccountId) || State.DesiredRevision is null) return;
        IsOperating = true; CurrentError = null;
        try { State.Apply(await platformApiClient.RemediateAccountPreferencesAsync(State.AccountId, State.DesiredRevision.Value, State.TrailingStopsEnabled, cancellationToken)); Message = "Account preferences remediation completed."; }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException) { CurrentError = CreateErrorMessage("Unable to remediate account preferences", exception); }
        finally { IsOperating = false; }
    }

    private static string CreateErrorMessage(string fallback, Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? $"{fallback}." : $"{fallback}. {exception.Message}";

    private async Task LoadHistoryAsync(string? cursor, CancellationToken cancellationToken)
    {
        var history = await platformApiClient.GetAccountPreferencesHistoryAsync(25, cursor, cancellationToken);
        State.ApplyHistory(history);
    }
}
