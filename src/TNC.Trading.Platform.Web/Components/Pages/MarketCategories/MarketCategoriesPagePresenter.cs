using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

/// <summary>
/// Coordinates market-category loading and operator refreshes while retaining the last saved view on failure.
/// </summary>
internal sealed class MarketCategoriesPagePresenter(PlatformApiClient platformApiClient)
{
    /// <summary>Gets the most recently saved market-category snapshot.</summary>
    public MarketCategoriesViewModel? State { get; private set; }

    /// <summary>Gets the current load or refresh failure message.</summary>
    public string? Error { get; private set; }

    /// <summary>Gets whether the initial snapshot is being loaded.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets whether an operator refresh is in progress.</summary>
    public bool IsRefreshing { get; private set; }

    /// <summary>Loads the saved market-category snapshot.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        try
        {
            State = await platformApiClient.GetMarketCategoriesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            Error = CreateErrorMessage("Unable to load market categories", exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Refreshes the provider snapshot and preserves saved data when the refresh fails.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        Error = null;
        try
        {
            State = await platformApiClient.RefreshMarketCategoriesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            Error = CreateErrorMessage("Unable to refresh market categories", exception);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private static string CreateErrorMessage(string fallback, Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? $"{fallback}." : $"{fallback}. {exception.Message}";
}
