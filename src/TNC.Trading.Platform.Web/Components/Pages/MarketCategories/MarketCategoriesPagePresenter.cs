using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

/// <summary>
/// Coordinates saved market-category loading and operator interest updates.
/// </summary>
internal sealed class MarketCategoriesPagePresenter(PlatformApiClient platformApiClient)
{
    private readonly SemaphoreSlim statusRefreshLock = new(1, 1);

    /// <summary>Gets the most recently saved market-category snapshot.</summary>
    public MarketCategoriesViewModel? State { get; private set; }

    /// <summary>Gets the current load failure message.</summary>
    public string? Error { get; private set; }

    /// <summary>Gets whether the initial snapshot is being loaded.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets the safe SQL-only collection status.</summary>
    public MarketCategoryInstrumentCollectionStatusViewModel? CollectionStatus { get; private set; }

    /// <summary>Gets a safe collector status read error without hiding the saved categories.</summary>
    public string? CollectionStatusError { get; private set; }

    /// <summary>Gets the category currently being saved.</summary>
    public string? SavingInterestCategoryCode { get; private set; }

    /// <summary>Gets the most recent interest update result message.</summary>
    public string? InterestMessage { get; private set; }

    /// <summary>Gets the most recent interest update error.</summary>
    public string? InterestError { get; private set; }

    /// <summary>Loads the saved market-category snapshot.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        try
        {
            State = await platformApiClient.GetMarketCategoriesAsync(cancellationToken);
            await RefreshCollectionStatusAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformApiException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            Error = CreateErrorMessage("Unable to load market categories", exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Updates one category using the current environment-wide interest revision.</summary>
    public async Task SetInterestAsync(
        string categoryCode,
        bool interested,
        CancellationToken cancellationToken)
    {
        var state = State;
        if (SavingInterestCategoryCode is not null
            || state?.InterestRevision is not { } expectedRevision)
        {
            return;
        }

        var category = state.Categories.FirstOrDefault(
            item => string.Equals(item.Code, categoryCode, StringComparison.Ordinal));
        if (category is null || category.Interested == interested)
        {
            return;
        }

        SavingInterestCategoryCode = categoryCode;
        InterestError = null;
        InterestMessage = null;
        try
        {
            var revision = await platformApiClient.UpdateMarketCategoryInterestAsync(
                categoryCode,
                interested,
                expectedRevision,
                cancellationToken);
            State = state with
            {
                InterestRevision = revision,
                Categories = state.Categories
                    .Select(item => string.Equals(item.Code, categoryCode, StringComparison.Ordinal)
                        ? item with { Interested = interested }
                        : item)
                    .ToArray()
            };
            InterestMessage = $"Interest saved for {categoryCode}.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (PlatformApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            InterestError = "Interest changed elsewhere. Reload the categories before saving another change.";
        }
        catch (Exception exception) when (exception is HttpRequestException or PlatformApiException or PlatformScopeChallengeRequiredException or InvalidOperationException)
        {
            InterestError = CreateErrorMessage("Unable to save category interest", exception);
        }
        finally
        {
            SavingInterestCategoryCode = null;
        }
    }

    /// <summary>Refreshes the schedule-derived status without reloading the saved category catalogue.</summary>
    public async Task RefreshCollectionStatusAsync(CancellationToken cancellationToken)
    {
        await statusRefreshLock.WaitAsync(cancellationToken);
        try
        {
            CollectionStatusError = null;
            try
            {
                CollectionStatus = await platformApiClient.GetInstrumentCollectionStatusAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (exception is HttpRequestException or PlatformApiException or PlatformScopeChallengeRequiredException or InvalidOperationException)
            {
                CollectionStatus = null;
                CollectionStatusError = CreateErrorMessage("Unable to load collector status", exception);
            }
        }
        finally
        {
            statusRefreshLock.Release();
        }
    }

    private static string CreateErrorMessage(string fallback, Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? $"{fallback}." : $"{fallback}. {exception.Message}";
}
