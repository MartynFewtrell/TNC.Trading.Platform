using System.Net;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class MarketDetailsPagePresenter(PlatformApiClient platformApiClient)
{
    private long loadSequence;
    private string? activeCategoryCode;
    private string? activeEpic;

    public MarketDetailViewModel? Detail { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsStale { get; private set; }

    public Task LoadAsync(
        string categoryCode,
        string epic,
        long? listingVersion,
        CancellationToken cancellationToken) =>
        LoadCoreAsync(categoryCode, epic, listingVersion, cancellationToken);

    public Task ReloadAsync(string categoryCode, string epic, CancellationToken cancellationToken) =>
        LoadCoreAsync(categoryCode, epic, null, cancellationToken);

    private async Task LoadCoreAsync(
        string categoryCode,
        string epic,
        long? listingVersion,
        CancellationToken cancellationToken)
    {
        var sequence = Interlocked.Increment(ref loadSequence);
        if (!string.Equals(activeCategoryCode, categoryCode, StringComparison.Ordinal)
            || !string.Equals(activeEpic, epic, StringComparison.Ordinal))
        {
            activeCategoryCode = categoryCode;
            activeEpic = epic;
            Detail = null;
            IsStale = false;
        }

        Error = null;
        IsLoading = true;
        try
        {
            var detail = await platformApiClient.GetMarketDetailAsync(
                categoryCode,
                epic,
                listingVersion,
                cancellationToken).ConfigureAwait(false);
            if (sequence == Volatile.Read(ref loadSequence))
            {
                Detail = detail;
                IsStale = false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (PlatformApiException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            if (sequence == Volatile.Read(ref loadSequence))
            {
                IsStale = true;
                Error = "The saved instrument listing changed. Reload saved market details to use the current listing.";
            }
        }
        catch (ArgumentOutOfRangeException exception)
        {
            if (sequence == Volatile.Read(ref loadSequence))
            {
                Error = CreateErrorMessage("The listing version is invalid", exception);
            }
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or PlatformApiException
                or PlatformScopeChallengeRequiredException
                or InvalidOperationException)
        {
            if (sequence == Volatile.Read(ref loadSequence))
            {
                Error = CreateErrorMessage("Unable to load saved market details", exception);
            }
        }
        finally
        {
            if (sequence == Volatile.Read(ref loadSequence))
            {
                IsLoading = false;
            }
        }
    }

    private static string CreateErrorMessage(string fallback, Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? $"{fallback}." : $"{fallback}. {exception.Message}";
}
