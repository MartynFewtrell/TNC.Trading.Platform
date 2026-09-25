using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class MarketCategoryInstrumentsPagePresenter(PlatformApiClient platformApiClient)
{
    private const int PageSize = 50;
    private readonly List<string?> cursors = [null];
    private string? activeCategoryCode;
    private int currentPageIndex;

    public MarketCategoryInstrumentPageViewModel? Page { get; private set; }
    public MarketCategoryViewModel? Category { get; private set; }
    public MarketCategoryInstrumentCollectionStatusViewModel? CollectionStatus { get; private set; }
    public string? Error { get; private set; }
    public string? MetadataError { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsStale { get; private set; }
    public int CurrentPageNumber => currentPageIndex + 1;
    public string PageCountDescription => CanGoNext ? $"at least {CurrentPageNumber + 1}" : CurrentPageNumber.ToString();
    public bool CanGoPrevious => currentPageIndex > 0;
    public bool CanGoNext => !string.IsNullOrWhiteSpace(Page?.NextCursor);

    public async Task LoadAsync(string categoryCode, CancellationToken cancellationToken)
    {
        if (!string.Equals(activeCategoryCode, categoryCode, StringComparison.Ordinal))
        {
            activeCategoryCode = categoryCode;
            Page = null;
            Category = null;
            CollectionStatus = null;
            cursors.Clear();
            cursors.Add(null);
            currentPageIndex = 0;
        }

        if (await LoadPageAsync(categoryCode, cursors[currentPageIndex], cancellationToken).ConfigureAwait(false))
        {
            await LoadMetadataAsync(categoryCode, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task LoadNextAsync(string categoryCode, CancellationToken cancellationToken)
    {
        if (!CanGoNext || IsLoading)
        {
            return;
        }

        var cursor = Page!.NextCursor!;
        if (await LoadPageAsync(categoryCode, cursor, cancellationToken).ConfigureAwait(false))
        {
            cursors.Add(cursor);
            currentPageIndex++;
        }
    }

    public async Task LoadPreviousAsync(string categoryCode, CancellationToken cancellationToken)
    {
        if (!CanGoPrevious || IsLoading)
        {
            return;
        }

        var previousIndex = currentPageIndex - 1;
        if (await LoadPageAsync(categoryCode, cursors[previousIndex], cancellationToken).ConfigureAwait(false))
        {
            currentPageIndex = previousIndex;
        }
    }

    public async Task ReloadAsync(string categoryCode, CancellationToken cancellationToken)
    {
        cursors.Clear();
        cursors.Add(null);
        currentPageIndex = 0;
        IsStale = false;
        await LoadAsync(categoryCode, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> LoadPageAsync(string categoryCode, string? cursor, CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        IsStale = false;
        try
        {
            Page = await platformApiClient.GetMarketCategoryInstrumentPageAsync(
                categoryCode,
                PageSize,
                cursor,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (PlatformApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            IsStale = true;
            Error = "The saved instrument snapshot changed. Reload from the first page to continue.";
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformApiException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            Error = CreateErrorMessage("Unable to load saved instruments", exception);
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadMetadataAsync(string categoryCode, CancellationToken cancellationToken)
    {
        MetadataError = null;
        try
        {
            var categories = await platformApiClient.GetMarketCategoriesAsync(cancellationToken).ConfigureAwait(false);
            Category = categories.Categories.FirstOrDefault(item =>
                string.Equals(item.Code, categoryCode, StringComparison.Ordinal));
            if (Category is null)
            {
                MetadataError = "Category details are not present in the current saved catalogue.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformApiException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            MetadataError = CreateErrorMessage("Unable to load category details", exception);
        }

        try
        {
            CollectionStatus = await platformApiClient.GetInstrumentCollectionStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformApiException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            MetadataError = string.IsNullOrWhiteSpace(MetadataError)
                ? CreateErrorMessage("Unable to load collection schedule", exception)
                : MetadataError;
        }
    }

    private static string CreateErrorMessage(string fallback, Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? $"{fallback}." : $"{fallback}. {exception.Message}";
}
