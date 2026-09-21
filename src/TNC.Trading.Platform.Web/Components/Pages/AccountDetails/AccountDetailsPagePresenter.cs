using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class AccountDetailsPagePresenter(PlatformApiClient platformApiClient)
{
    public AccountDetailsResponseViewModel? State { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsRefreshing { get; private set; }

    public async Task LoadAsync(string? cursor = null)
    {
        IsLoading = true;
        Error = null;
        try
        {
            State = await platformApiClient.GetAccountDetailsAsync(cursor, CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            Error = $"Unable to load account details: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task LoadOlderAsync() => State?.OlderCursor is { } cursor ? LoadAsync(cursor) : Task.CompletedTask;

    public Task LoadNewerAsync() => State?.NewerCursor is { } cursor ? LoadAsync(cursor) : Task.CompletedTask;

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        Error = null;
        try
        {
            var retrieval = await platformApiClient.RefreshAccountDetailsAsync(CancellationToken.None);
            State = new AccountDetailsResponseViewModel(retrieval, null, State?.NewerCursor);
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            Error = $"Unable to refresh account details: {exception.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}