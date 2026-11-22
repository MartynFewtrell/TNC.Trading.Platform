using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Features.MarketData;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgMarketDataGateway(IgRestGatewayClient restClient) : IMarketDataGateway
{
    private const int MaximumEpicsPerRequest = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MarketDataGatewayOutcome<MarketDetails>> GetMarketDetailsAsync(
        string epic,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(epic))
        {
            return Failure<MarketDetails>(MarketDataFailureCategory.Rejected, "IG market epic is invalid.");
        }

        var exchange = await restClient.SendMarketDataAsync(
            HttpMethod.Get,
            $"markets/{Uri.EscapeDataString(epic)}",
            "2",
            cancellationToken).ConfigureAwait(false);

        return Map(exchange, ParseMarketDetails);
    }

    public async Task<MarketDataGatewayOutcome<IReadOnlyList<MarketDetails>>> GetMarketDetailsAsync(
        IReadOnlyCollection<string> epics,
        CancellationToken cancellationToken)
    {
        if (epics is null
            || epics.Count is < 1 or > MaximumEpicsPerRequest
            || epics.Any(epic => !IsValidIdentifier(epic))
            || epics.Distinct(StringComparer.Ordinal).Count() != epics.Count)
        {
            return Failure<IReadOnlyList<MarketDetails>>(MarketDataFailureCategory.Rejected, "IG market epic collection is invalid.");
        }

        var encodedEpics = string.Join(",", epics.Select(Uri.EscapeDataString));
        var exchange = await restClient.SendMarketDataAsync(
            HttpMethod.Get,
            $"markets?epics={encodedEpics}",
            "1",
            cancellationToken).ConfigureAwait(false);

        return Map(exchange, ParseMarketDetailsList);
    }

    public async Task<MarketDataGatewayOutcome<IReadOnlyList<MarketListing>>> SearchMarketsAsync(
        string searchTerm,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length > 128)
        {
            return Failure<IReadOnlyList<MarketListing>>(MarketDataFailureCategory.Rejected, "IG market search term is invalid.");
        }

        var exchange = await restClient.SendMarketDataAsync(
            HttpMethod.Get,
            $"markets?searchTerm={Uri.EscapeDataString(searchTerm)}",
            "1",
            cancellationToken).ConfigureAwait(false);

        return Map(exchange, ParseMarketListings);
    }

    public async Task<MarketDataGatewayOutcome<IReadOnlyList<MarketPriceBar>>> GetPricesByDateRangeAsync(
        string epic,
        string resolution,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(epic)
            || !IsValidIdentifier(resolution)
            || end <= start)
        {
            return Failure<IReadOnlyList<MarketPriceBar>>(MarketDataFailureCategory.Rejected, "IG price range request is invalid.");
        }

        var from = Uri.EscapeDataString(start.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        var to = Uri.EscapeDataString(end.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        var exchange = await restClient.SendMarketDataAsync(
            HttpMethod.Get,
            $"prices/{Uri.EscapeDataString(epic)}/{Uri.EscapeDataString(resolution)}/{from}/{to}",
            "2",
            cancellationToken).ConfigureAwait(false);

        return Map(exchange, ParsePrices);
    }

    public async Task<MarketDataGatewayOutcome<IReadOnlyList<MarketPriceBar>>> GetPricesByPointCountAsync(
        string epic,
        string resolution,
        int pointCount,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(epic) || !IsValidIdentifier(resolution) || pointCount < 1)
        {
            return Failure<IReadOnlyList<MarketPriceBar>>(MarketDataFailureCategory.Rejected, "IG price point-count request is invalid.");
        }

        var exchange = await restClient.SendMarketDataAsync(
            HttpMethod.Get,
            $"prices/{Uri.EscapeDataString(epic)}/{Uri.EscapeDataString(resolution)}/{pointCount.ToString(CultureInfo.InvariantCulture)}",
            "2",
            cancellationToken).ConfigureAwait(false);

        return Map(exchange, ParsePrices);
    }

    private static MarketDataGatewayOutcome<T> Map<T>(
        IgRestGatewayClient.ExchangeResult exchange,
        Func<string, T?> deserialize)
    {
        if (exchange is IgRestGatewayClient.ExchangeResult.Failed failed)
        {
            return Failure<T>(MapFailure(failed.Category), SafeReason(failed.Category));
        }

        var response = (IgRestGatewayClient.ExchangeResult.Completed)exchange;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Failure<T>(MarketDataFailureCategory.Unauthorized, "IG market data request was unauthorized.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Failure<T>(MarketDataFailureCategory.RateLimited, "IG market data request was rate limited.");
        }

        if (response.StatusCode == HttpStatusCode.RequestTimeout)
        {
            return Failure<T>(MarketDataFailureCategory.Timeout, "IG market data request timed out.");
        }

        if ((int)response.StatusCode >= 500)
        {
            return Failure<T>(MarketDataFailureCategory.Unavailable, "IG market data service is unavailable.");
        }

        if ((int)response.StatusCode is < 200 or > 299)
        {
            return Failure<T>(MarketDataFailureCategory.Rejected, "IG market data request was rejected.");
        }

        try
        {
            var value = deserialize(response.Body);
            return value is null
                ? Failure<T>(MarketDataFailureCategory.MalformedProviderData, "IG market data response was malformed.")
                : new MarketDataGatewayOutcome<T>.Succeeded(value, DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Failure<T>(MarketDataFailureCategory.MalformedProviderData, "IG market data response was malformed.");
        }
    }

    private static MarketDetails? ParseMarketDetails(string body)
    {
        var response = JsonSerializer.Deserialize<IgMarketDetailsResponse>(body, JsonOptions);
        if (response?.Instrument is null
            || response.Snapshot is null
            || string.IsNullOrWhiteSpace(response.Instrument.Epic)
            || string.IsNullOrWhiteSpace(response.Instrument.Name)
            || string.IsNullOrWhiteSpace(response.Instrument.Type)
            || string.IsNullOrWhiteSpace(response.Snapshot.MarketStatus))
        {
            return null;
        }

        return MapMarketDetails(response.Instrument.Epic, response.Instrument.Name, response.Instrument.Type, response.Snapshot);
    }

    private static IReadOnlyList<MarketDetails>? ParseMarketDetailsList(string body)
    {
        var response = JsonSerializer.Deserialize<IgMarketDetailsListResponse>(body, JsonOptions);
        if (response?.MarketDetails is null)
        {
            return null;
        }

        var result = new List<MarketDetails>(response.MarketDetails.Count);
        foreach (var detail in response.MarketDetails)
        {
            if (detail.Instrument is null
                || detail.Snapshot is null
                || string.IsNullOrWhiteSpace(detail.Instrument.Epic)
                || string.IsNullOrWhiteSpace(detail.Instrument.Name)
                || string.IsNullOrWhiteSpace(detail.Instrument.Type)
                || string.IsNullOrWhiteSpace(detail.Snapshot.MarketStatus))
            {
                return null;
            }

            result.Add(MapMarketDetails(detail.Instrument.Epic, detail.Instrument.Name, detail.Instrument.Type, detail.Snapshot));
        }

        return result;
    }

    private static IReadOnlyList<MarketListing>? ParseMarketListings(string body)
    {
        var response = JsonSerializer.Deserialize<IgMarketSearchResponse>(body, JsonOptions);
        if (response?.Markets is null)
        {
            return null;
        }

        var result = new List<MarketListing>(response.Markets.Count);
        foreach (var market in response.Markets)
        {
            if (string.IsNullOrWhiteSpace(market.Epic)
                || string.IsNullOrWhiteSpace(market.InstrumentName)
                || string.IsNullOrWhiteSpace(market.InstrumentType)
                || string.IsNullOrWhiteSpace(market.MarketStatus))
            {
                return null;
            }

            result.Add(new MarketListing(
                market.Epic,
                market.InstrumentName,
                market.InstrumentType,
                market.Expiry,
                market.MarketStatus,
                market.Bid,
                market.Offer,
                market.High,
                market.Low,
                market.NetChange,
                market.PercentageChange,
                market.UpdateTime,
                market.DelayTime,
                market.ScalingFactor));
        }

        return result;
    }

    private static IReadOnlyList<MarketPriceBar>? ParsePrices(string body)
    {
        var response = JsonSerializer.Deserialize<IgPricesResponse>(body, JsonOptions);
        if (response?.Prices is null)
        {
            return null;
        }

        var result = new List<MarketPriceBar>(response.Prices.Count);
        foreach (var price in response.Prices)
        {
            if (string.IsNullOrWhiteSpace(price.SnapshotTime))
            {
                return null;
            }

            result.Add(new MarketPriceBar(
                price.SnapshotTime,
                price.OpenPrice?.Bid,
                price.OpenPrice?.Ask,
                price.OpenPrice?.LastTraded,
                price.ClosePrice?.Bid,
                price.ClosePrice?.Ask,
                price.ClosePrice?.LastTraded,
                price.HighPrice?.Bid,
                price.HighPrice?.Ask,
                price.HighPrice?.LastTraded,
                price.LowPrice?.Bid,
                price.LowPrice?.Ask,
                price.LowPrice?.LastTraded,
                price.LastTradedVolume));
        }

        return result;
    }

    private static MarketDetails MapMarketDetails(
        string epic,
        string name,
        string instrumentType,
        IgMarketSnapshot snapshot) =>
        new(
            epic,
            name,
            instrumentType,
            snapshot.MarketStatus!,
            snapshot.Bid,
            snapshot.Offer,
            snapshot.High,
            snapshot.Low,
            snapshot.NetChange,
            snapshot.PercentageChange,
            snapshot.UpdateTime,
            snapshot.DelayTime,
            snapshot.ScalingFactor);

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && !value.Contains('/');

    private static MarketDataFailureCategory MapFailure(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => MarketDataFailureCategory.UnsupportedEnvironment,
            IgRestGatewayClient.FailureCategory.Unauthorized => MarketDataFailureCategory.Unauthorized,
            IgRestGatewayClient.FailureCategory.RateLimited => MarketDataFailureCategory.RateLimited,
            IgRestGatewayClient.FailureCategory.Unavailable => MarketDataFailureCategory.Unavailable,
            IgRestGatewayClient.FailureCategory.Timeout => MarketDataFailureCategory.Timeout,
            IgRestGatewayClient.FailureCategory.MalformedProviderData => MarketDataFailureCategory.MalformedProviderData,
            _ => MarketDataFailureCategory.Rejected
        };

    private static string SafeReason(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => "The applied broker environment is unavailable.",
            IgRestGatewayClient.FailureCategory.Unauthorized => "IG market data request was unauthorized.",
            IgRestGatewayClient.FailureCategory.RateLimited => "IG market data request was rate limited.",
            IgRestGatewayClient.FailureCategory.Unavailable => "IG market data service is unavailable.",
            IgRestGatewayClient.FailureCategory.Timeout => "IG market data request timed out.",
            IgRestGatewayClient.FailureCategory.MalformedProviderData => "IG market data response was malformed.",
            _ => "IG market data request was rejected."
        };

    private static MarketDataGatewayOutcome<T> Failure<T>(MarketDataFailureCategory category, string reason) =>
        new MarketDataGatewayOutcome<T>.Failed(category, reason);

    private sealed record IgMarketDetailsResponse(
        [property: JsonPropertyName("instrument")] IgInstrument? Instrument,
        [property: JsonPropertyName("snapshot")] IgMarketSnapshot? Snapshot);

    private sealed record IgMarketDetailsListResponse(
        [property: JsonPropertyName("marketDetails")] List<IgMarketDetailsResponse>? MarketDetails);

    private sealed record IgMarketSearchResponse(
        [property: JsonPropertyName("markets")] List<IgMarketListing>? Markets);

    private sealed record IgInstrument(
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("type")] string? Type);

    private sealed record IgMarketSnapshot(
        [property: JsonPropertyName("marketStatus")] string? MarketStatus,
        [property: JsonPropertyName("netChange")] decimal? NetChange,
        [property: JsonPropertyName("percentageChange")] decimal? PercentageChange,
        [property: JsonPropertyName("updateTime")] string? UpdateTime,
        [property: JsonPropertyName("delayTime")] int DelayTime,
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("offer")] decimal? Offer,
        [property: JsonPropertyName("high")] decimal? High,
        [property: JsonPropertyName("low")] decimal? Low,
        [property: JsonPropertyName("scalingFactor")] int ScalingFactor);

    private sealed record IgMarketListing(
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("instrumentName")] string? InstrumentName,
        [property: JsonPropertyName("instrumentType")] string? InstrumentType,
        [property: JsonPropertyName("expiry")] string? Expiry,
        [property: JsonPropertyName("high")] decimal? High,
        [property: JsonPropertyName("low")] decimal? Low,
        [property: JsonPropertyName("percentageChange")] decimal? PercentageChange,
        [property: JsonPropertyName("netChange")] decimal? NetChange,
        [property: JsonPropertyName("updateTime")] string? UpdateTime,
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("offer")] decimal? Offer,
        [property: JsonPropertyName("delayTime")] int DelayTime,
        [property: JsonPropertyName("marketStatus")] string? MarketStatus,
        [property: JsonPropertyName("scalingFactor")] int ScalingFactor);

    private sealed record IgPricesResponse(
        [property: JsonPropertyName("prices")] List<IgPriceSnapshot>? Prices);

    private sealed record IgPriceSnapshot(
        [property: JsonPropertyName("snapshotTime")] string? SnapshotTime,
        [property: JsonPropertyName("openPrice")] IgPriceValues? OpenPrice,
        [property: JsonPropertyName("closePrice")] IgPriceValues? ClosePrice,
        [property: JsonPropertyName("highPrice")] IgPriceValues? HighPrice,
        [property: JsonPropertyName("lowPrice")] IgPriceValues? LowPrice,
        [property: JsonPropertyName("lastTradedVolume")] decimal? LastTradedVolume);

    private sealed record IgPriceValues(
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("ask")] decimal? Ask,
        [property: JsonPropertyName("lastTraded")] decimal? LastTraded);
}
