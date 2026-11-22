using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Features.Watchlists;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgWatchlistsGateway(IgRestGatewayClient restClient) : IWatchlistsGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WatchlistGatewayOutcome<IReadOnlyList<Watchlist>>> ListAsync(
        string expectedAccountId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId))
        {
            return Failure<IReadOnlyList<Watchlist>>(WatchlistFailureCategory.Rejected, "The expected IG account is invalid.");
        }

        var exchange = await SendAsync(
            HttpMethod.Get,
            "watchlists",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapRead(exchange, ParseWatchlists);
    }

    public async Task<WatchlistGatewayOutcome<IReadOnlyList<WatchlistMarket>>> GetMarketsAsync(
        string expectedAccountId,
        string watchlistId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidIdentifier(watchlistId))
        {
            return Failure<IReadOnlyList<WatchlistMarket>>(WatchlistFailureCategory.Rejected, "The IG account or watchlist identifier is invalid.");
        }

        var exchange = await SendAsync(
            HttpMethod.Get,
            $"watchlists/{Uri.EscapeDataString(watchlistId)}",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapRead(exchange, ParseWatchlistMarkets);
    }

    public async Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> CreateAsync(
        string expectedAccountId,
        string name,
        IReadOnlyList<string> epics,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId)
            || string.IsNullOrWhiteSpace(name)
            || epics is null
            || epics.Any(epic => !IsValidIdentifier(epic))
            || epics.Distinct(StringComparer.Ordinal).Count() != epics.Count)
        {
            return Failure<WatchlistMutationAcknowledgement>(WatchlistFailureCategory.Rejected, "IG watchlist creation request is invalid.");
        }

        var exchange = await SendAsync(
            HttpMethod.Post,
            "watchlists",
            new IgCreateWatchlistBody(name, epics),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapCreate(exchange);
    }

    public async Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> DeleteAsync(
        string expectedAccountId,
        string watchlistId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidIdentifier(watchlistId))
        {
            return Failure<WatchlistMutationAcknowledgement>(WatchlistFailureCategory.Rejected, "The IG account or watchlist identifier is invalid.");
        }

        var exchange = await SendAsync(
            HttpMethod.Delete,
            $"watchlists/{Uri.EscapeDataString(watchlistId)}",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> AddMarketAsync(
        string expectedAccountId,
        string watchlistId,
        string epic,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId)
            || !IsValidIdentifier(watchlistId)
            || !IsValidIdentifier(epic))
        {
            return Failure<WatchlistMutationAcknowledgement>(WatchlistFailureCategory.Rejected, "The IG account, watchlist, or market identifier is invalid.");
        }

        var exchange = await SendAsync(
            HttpMethod.Put,
            $"watchlists/{Uri.EscapeDataString(watchlistId)}",
            new IgAddWatchlistMarketBody(epic),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>> RemoveMarketAsync(
        string expectedAccountId,
        string watchlistId,
        string epic,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId)
            || !IsValidIdentifier(watchlistId)
            || !IsValidIdentifier(epic))
        {
            return Failure<WatchlistMutationAcknowledgement>(WatchlistFailureCategory.Rejected, "The IG account, watchlist, or market identifier is invalid.");
        }

        var exchange = await SendAsync(
            HttpMethod.Delete,
            $"watchlists/{Uri.EscapeDataString(watchlistId)}/{Uri.EscapeDataString(epic)}",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    private Task<IgRestGatewayClient.ExchangeResult> SendAsync(
        HttpMethod method,
        string resource,
        object? body,
        string expectedAccountId,
        CancellationToken cancellationToken) =>
        restClient.SendDemoAccountAsync(
            method,
            resource,
            "1",
            body,
            expectedAccountId,
            cancellationToken);

    private static WatchlistGatewayOutcome<T> MapRead<T>(
        IgRestGatewayClient.ExchangeResult exchange,
        Func<string, T?> deserialize)
    {
        if (exchange is IgRestGatewayClient.ExchangeResult.Failed failed)
        {
            return Failure<T>(MapFailure(failed.Category), SafeReason(failed.Category));
        }

        var response = (IgRestGatewayClient.ExchangeResult.Completed)exchange;
        if ((int)response.StatusCode is < 200 or > 299)
        {
            return Failure<T>(MapFailure(response.StatusCode), SafeReason(response.StatusCode));
        }

        try
        {
            var value = deserialize(response.Body);
            return value is null
                ? Failure<T>(WatchlistFailureCategory.MalformedProviderData, "IG watchlist response was malformed.")
                : new WatchlistGatewayOutcome<T>.Succeeded(value, DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Failure<T>(WatchlistFailureCategory.MalformedProviderData, "IG watchlist response was malformed.");
        }
    }

    private static WatchlistGatewayOutcome<WatchlistMutationAcknowledgement> MapCreate(
        IgRestGatewayClient.ExchangeResult exchange)
    {
        if (exchange is IgRestGatewayClient.ExchangeResult.Failed failed)
        {
            var category = MapFailure(failed.Category);
            return IsIndeterminate(category)
                ? Indeterminate(category, SafeReason(failed.Category))
                : Failure<WatchlistMutationAcknowledgement>(category, SafeReason(failed.Category));
        }

        var response = (IgRestGatewayClient.ExchangeResult.Completed)exchange;
        if ((int)response.StatusCode is < 200 or > 299)
        {
            var category = MapFailure(response.StatusCode);
            return IsIndeterminate(category)
                ? Indeterminate(category, SafeReason(response.StatusCode))
                : Failure<WatchlistMutationAcknowledgement>(category, SafeReason(response.StatusCode));
        }

        try
        {
            var body = JsonSerializer.Deserialize<IgCreateWatchlistResponse>(response.Body, JsonOptions);
            if (body is null
                || string.IsNullOrWhiteSpace(body.WatchlistId)
                || body.Status is not ("SUCCESS" or "SUCCESS_NOT_ALL_INSTRUMENTS_ADDED"))
            {
                return Indeterminate(WatchlistFailureCategory.MalformedProviderData, "IG accepted a watchlist request but returned an incomplete acknowledgement.");
            }

            return new WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>.Succeeded(
                new WatchlistMutationAcknowledgement(
                    body.WatchlistId,
                    string.Equals(body.Status, "SUCCESS_NOT_ALL_INSTRUMENTS_ADDED", StringComparison.Ordinal)),
                DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Indeterminate(WatchlistFailureCategory.MalformedProviderData, "IG accepted a watchlist request but its acknowledgement was malformed.");
        }
    }

    private static WatchlistGatewayOutcome<WatchlistMutationAcknowledgement> MapMutation(
        IgRestGatewayClient.ExchangeResult exchange)
    {
        if (exchange is IgRestGatewayClient.ExchangeResult.Failed failed)
        {
            var category = MapFailure(failed.Category);
            return IsIndeterminate(category)
                ? Indeterminate(category, SafeReason(failed.Category))
                : Failure<WatchlistMutationAcknowledgement>(category, SafeReason(failed.Category));
        }

        var response = (IgRestGatewayClient.ExchangeResult.Completed)exchange;
        if ((int)response.StatusCode is < 200 or > 299)
        {
            var category = MapFailure(response.StatusCode);
            return IsIndeterminate(category)
                ? Indeterminate(category, SafeReason(response.StatusCode))
                : Failure<WatchlistMutationAcknowledgement>(category, SafeReason(response.StatusCode));
        }

        return new WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>.Succeeded(
            new WatchlistMutationAcknowledgement(null, false),
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<Watchlist>? ParseWatchlists(string body)
    {
        var response = JsonSerializer.Deserialize<IgWatchlistsResponse>(body, JsonOptions);
        if (response?.Watchlists is null)
        {
            return null;
        }

        var watchlists = new List<Watchlist>(response.Watchlists.Count);
        foreach (var item in response.Watchlists)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name))
            {
                return null;
            }

            watchlists.Add(new Watchlist(item.Id, item.Name, item.Editable, item.Deleteable, item.DefaultSystemWatchlist));
        }

        return watchlists;
    }

    private static IReadOnlyList<WatchlistMarket>? ParseWatchlistMarkets(string body)
    {
        var response = JsonSerializer.Deserialize<IgWatchlistMarketsResponse>(body, JsonOptions);
        if (response?.Markets is null)
        {
            return null;
        }

        var markets = new List<WatchlistMarket>(response.Markets.Count);
        foreach (var item in response.Markets)
        {
            if (string.IsNullOrWhiteSpace(item.Epic)
                || string.IsNullOrWhiteSpace(item.InstrumentName)
                || string.IsNullOrWhiteSpace(item.InstrumentType)
                || string.IsNullOrWhiteSpace(item.MarketStatus))
            {
                return null;
            }

            markets.Add(new WatchlistMarket(
                item.Epic,
                item.InstrumentName,
                item.InstrumentType,
                item.Expiry,
                item.MarketStatus,
                item.Bid,
                item.Offer,
                item.High,
                item.Low,
                item.NetChange,
                item.PercentageChange,
                item.UpdateTime));
        }

        return markets;
    }

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && !value.Contains('/');

    private static bool IsIndeterminate(WatchlistFailureCategory category) =>
        category is WatchlistFailureCategory.Unavailable
            or WatchlistFailureCategory.Timeout
            or WatchlistFailureCategory.MalformedProviderData;

    private static WatchlistFailureCategory MapFailure(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => WatchlistFailureCategory.UnsupportedEnvironment,
            IgRestGatewayClient.FailureCategory.Unauthorized => WatchlistFailureCategory.Unauthorized,
            IgRestGatewayClient.FailureCategory.RateLimited => WatchlistFailureCategory.RateLimited,
            IgRestGatewayClient.FailureCategory.Unavailable => WatchlistFailureCategory.Unavailable,
            IgRestGatewayClient.FailureCategory.Timeout => WatchlistFailureCategory.Timeout,
            IgRestGatewayClient.FailureCategory.MalformedProviderData => WatchlistFailureCategory.MalformedProviderData,
            _ => WatchlistFailureCategory.Rejected
        };

    private static WatchlistFailureCategory MapFailure(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => WatchlistFailureCategory.Unauthorized,
            HttpStatusCode.TooManyRequests => WatchlistFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => WatchlistFailureCategory.Timeout,
            >= HttpStatusCode.InternalServerError => WatchlistFailureCategory.Unavailable,
            _ => WatchlistFailureCategory.Rejected
        };

    private static string SafeReason(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => "The applied IG Demo account is unavailable or does not match the requested account.",
            IgRestGatewayClient.FailureCategory.Unauthorized => "IG watchlist request was unauthorized.",
            IgRestGatewayClient.FailureCategory.RateLimited => "IG watchlist request was rate limited.",
            IgRestGatewayClient.FailureCategory.Unavailable => "IG watchlist service is unavailable; the operation may have completed.",
            IgRestGatewayClient.FailureCategory.Timeout => "IG watchlist request timed out; the operation may have completed.",
            IgRestGatewayClient.FailureCategory.MalformedProviderData => "IG watchlist response was malformed.",
            _ => "IG watchlist request was rejected."
        };

    private static string SafeReason(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "IG watchlist request was unauthorized.",
            HttpStatusCode.TooManyRequests => "IG watchlist request was rate limited.",
            HttpStatusCode.RequestTimeout => "IG watchlist request timed out; the operation may have completed.",
            >= HttpStatusCode.InternalServerError => "IG watchlist service is unavailable; the operation may have completed.",
            _ => "IG watchlist request was rejected."
        };

    private static WatchlistGatewayOutcome<T> Failure<T>(WatchlistFailureCategory category, string safeReason) =>
        new WatchlistGatewayOutcome<T>.Failed(category, safeReason);

    private static WatchlistGatewayOutcome<WatchlistMutationAcknowledgement> Indeterminate(
        WatchlistFailureCategory category,
        string safeReason) =>
        new WatchlistGatewayOutcome<WatchlistMutationAcknowledgement>.Indeterminate(category, safeReason);

    private sealed record IgWatchlistsResponse(
        [property: JsonPropertyName("watchlists")] List<IgWatchlist>? Watchlists);

    private sealed record IgWatchlist(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("editable")] bool Editable,
        [property: JsonPropertyName("deleteable")] bool Deleteable,
        [property: JsonPropertyName("defaultSystemWatchlist")] bool DefaultSystemWatchlist);

    private sealed record IgWatchlistMarketsResponse(
        [property: JsonPropertyName("markets")] List<IgWatchlistMarket>? Markets);

    private sealed record IgWatchlistMarket(
        [property: JsonPropertyName("instrumentName")] string? InstrumentName,
        [property: JsonPropertyName("expiry")] string? Expiry,
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("instrumentType")] string? InstrumentType,
        [property: JsonPropertyName("marketStatus")] string? MarketStatus,
        [property: JsonPropertyName("high")] decimal? High,
        [property: JsonPropertyName("low")] decimal? Low,
        [property: JsonPropertyName("percentageChange")] decimal? PercentageChange,
        [property: JsonPropertyName("netChange")] decimal? NetChange,
        [property: JsonPropertyName("updateTime")] string? UpdateTime,
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("offer")] decimal? Offer);

    private sealed record IgCreateWatchlistBody(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("epics")] IReadOnlyList<string> Epics);

    private sealed record IgAddWatchlistMarketBody(
        [property: JsonPropertyName("epic")] string Epic);

    private sealed record IgCreateWatchlistResponse(
        [property: JsonPropertyName("watchlistId")] string? WatchlistId,
        [property: JsonPropertyName("status")] string? Status);
}
