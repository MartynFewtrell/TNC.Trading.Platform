namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class AccountPreferencesPagePresenter(PlatformApiClient platformApiClient)
{
    public AccountPreferencesPageViewModel State { get; } = new();
    public bool IsLoading { get; private set; }
    public bool IsSaving { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        try
        {
            State.Apply(await platformApiClient.GetAccountPreferencesAsync(cancellationToken));
            await LoadHistoryAsync(null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            Error = "Unable to load account preferences.";
        }
        finally { IsLoading = false; }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;
        Error = null;
        Message = null;
        try
        {
            State.Apply(await platformApiClient.UpdateAccountPreferencesAsync(State.TrailingStopsEnabled, cancellationToken));
            Message = "Trailing stops preference saved and confirmed.";
            await LoadHistoryAsync(null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            Error = "Unable to save and confirm account preferences.";
        }
        finally { IsSaving = false; }
    }

    public Task LoadNextHistoryPageAsync(CancellationToken cancellationToken) => LoadHistoryAsync(State.NextCursor, cancellationToken);

    private async Task LoadHistoryAsync(string? cursor, CancellationToken cancellationToken)
    {
        var history = await platformApiClient.GetAccountPreferencesHistoryAsync(25, cursor, cancellationToken);
        State.ApplyHistory(history);
    }
}
