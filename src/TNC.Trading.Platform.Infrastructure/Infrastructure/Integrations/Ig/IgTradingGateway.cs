using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Features.Trading;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgTradingGateway(IgRestGatewayClient restClient) : ITradingGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TradingGatewayOutcome<IReadOnlyList<OpenPosition>>> ListOpenPositionsAsync(
        string expectedAccountId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId))
        {
            return Failure<IReadOnlyList<OpenPosition>>(TradingFailureCategory.Rejected, "The expected IG account is invalid.");
        }

        var exchange = await SendReadAsync(
            "positions",
            "2",
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapRead(exchange, ParsePositions);
    }

    public async Task<TradingGatewayOutcome<OpenPosition>> GetOpenPositionAsync(
        string expectedAccountId,
        string dealId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidIdentifier(dealId))
        {
            return Failure<OpenPosition>(TradingFailureCategory.Rejected, "The IG account or deal identifier is invalid.");
        }

        var exchange = await SendReadAsync(
            $"positions/{Uri.EscapeDataString(dealId)}",
            "2",
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapRead(exchange, ParsePosition);
    }

    public async Task<TradingGatewayOutcome<IReadOnlyList<WorkingOrder>>> ListWorkingOrdersAsync(
        string expectedAccountId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId))
        {
            return Failure<IReadOnlyList<WorkingOrder>>(TradingFailureCategory.Rejected, "The expected IG account is invalid.");
        }

        var exchange = await SendReadAsync(
            "workingorders",
            "2",
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapRead(exchange, ParseWorkingOrders);
    }

    public async Task<TradingGatewayOutcome<DealConfirmation>> GetDealConfirmationAsync(
        string expectedAccountId,
        string dealReference,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidIdentifier(dealReference))
        {
            return Failure<DealConfirmation>(TradingFailureCategory.Rejected, "The IG account or deal reference is invalid.");
        }

        var exchange = await SendReadAsync(
            $"confirms/{Uri.EscapeDataString(dealReference)}",
            "1",
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapRead(exchange, ParseConfirmation);
    }

    public async Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> CreatePositionAsync(
        string expectedAccountId,
        PositionCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidPositionCreateRequest(request))
        {
            return Failure<TradingMutationAcknowledgement>(TradingFailureCategory.Rejected, "IG position request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Post,
            "positions/otc",
            "2",
            new IgPositionCreateBody(request),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> AmendPositionAsync(
        string expectedAccountId,
        string dealId,
        PositionAmendmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidIdentifier(dealId) || request is null)
        {
            return Failure<TradingMutationAcknowledgement>(TradingFailureCategory.Rejected, "IG position amendment request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Put,
            $"positions/otc/{Uri.EscapeDataString(dealId)}",
            "2",
            new IgPositionAmendmentBody(request),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> ClosePositionAsync(
        string expectedAccountId,
        PositionCloseRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidPositionCloseRequest(request))
        {
            return Failure<TradingMutationAcknowledgement>(TradingFailureCategory.Rejected, "IG position close request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Delete,
            "positions/otc",
            "1",
            new IgPositionCloseBody(request),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> CreateWorkingOrderAsync(
        string expectedAccountId,
        WorkingOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidWorkingOrderCreateRequest(request))
        {
            return Failure<TradingMutationAcknowledgement>(TradingFailureCategory.Rejected, "IG working-order request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Post,
            "workingorders/otc",
            "2",
            new IgWorkingOrderCreateBody(request),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> AmendWorkingOrderAsync(
        string expectedAccountId,
        string dealId,
        WorkingOrderAmendmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId)
            || !IsValidIdentifier(dealId)
            || request is null
            || string.IsNullOrWhiteSpace(request.TimeInForce)
            || string.IsNullOrWhiteSpace(request.GoodTillDate)
            || string.IsNullOrWhiteSpace(request.OrderType)
            || request.Level <= 0)
        {
            return Failure<TradingMutationAcknowledgement>(TradingFailureCategory.Rejected, "IG working-order amendment request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Put,
            $"workingorders/otc/{Uri.EscapeDataString(dealId)}",
            "2",
            new IgWorkingOrderAmendmentBody(request),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    public async Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> CancelWorkingOrderAsync(
        string expectedAccountId,
        string dealId,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || !IsValidIdentifier(dealId))
        {
            return Failure<TradingMutationAcknowledgement>(TradingFailureCategory.Rejected, "The IG account or working-order deal identifier is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Delete,
            $"workingorders/otc/{Uri.EscapeDataString(dealId)}",
            "1",
            new IgEmptyBody(),
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return MapMutation(exchange);
    }

    private Task<IgRestGatewayClient.ExchangeResult> SendReadAsync(
        string resource,
        string version,
        string expectedAccountId,
        CancellationToken cancellationToken) =>
        restClient.SendDemoAccountAsync(
            HttpMethod.Get,
            resource,
            version,
            null,
            expectedAccountId,
            cancellationToken);

    private static TradingGatewayOutcome<T> MapRead<T>(
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
                ? Failure<T>(TradingFailureCategory.MalformedProviderData, "IG trading response was malformed.")
                : new TradingGatewayOutcome<T>.Succeeded(value, DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Failure<T>(TradingFailureCategory.MalformedProviderData, "IG trading response was malformed.");
        }
    }

    private static TradingGatewayOutcome<TradingMutationAcknowledgement> MapMutation(
        IgRestGatewayClient.ExchangeResult exchange)
    {
        if (exchange is IgRestGatewayClient.ExchangeResult.Failed failed)
        {
            var category = MapFailure(failed.Category);
            return IsIndeterminate(category)
                ? Indeterminate(category, SafeReason(failed.Category))
                : Failure<TradingMutationAcknowledgement>(category, SafeReason(failed.Category));
        }

        var response = (IgRestGatewayClient.ExchangeResult.Completed)exchange;
        if ((int)response.StatusCode is < 200 or > 299)
        {
            var category = MapFailure(response.StatusCode);
            return IsIndeterminate(category)
                ? Indeterminate(category, SafeReason(response.StatusCode))
                : Failure<TradingMutationAcknowledgement>(category, SafeReason(response.StatusCode));
        }

        try
        {
            var body = JsonSerializer.Deserialize<IgDealReferenceResponse>(response.Body, JsonOptions);
            if (string.IsNullOrWhiteSpace(body?.DealReference))
            {
                return Indeterminate(TradingFailureCategory.MalformedProviderData, "IG accepted a trading request but did not return a deal reference.");
            }

            return new TradingGatewayOutcome<TradingMutationAcknowledgement>.Succeeded(
                new TradingMutationAcknowledgement(body.DealReference),
                DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Indeterminate(TradingFailureCategory.MalformedProviderData, "IG accepted a trading request but its acknowledgement was malformed.");
        }
    }

    private static IReadOnlyList<OpenPosition>? ParsePositions(string body)
    {
        var response = JsonSerializer.Deserialize<IgPositionsResponse>(body, JsonOptions);
        if (response?.Positions is null)
        {
            return null;
        }

        var positions = new List<OpenPosition>(response.Positions.Count);
        foreach (var position in response.Positions)
        {
            var mapped = MapPosition(position);
            if (mapped is null)
            {
                return null;
            }

            positions.Add(mapped);
        }

        return positions;
    }

    private static OpenPosition? ParsePosition(string body)
    {
        var position = JsonSerializer.Deserialize<IgOpenPosition>(body, JsonOptions);
        return position is null ? null : MapPosition(position);
    }

    private static OpenPosition? MapPosition(IgOpenPosition position)
    {
        var data = position.Position;
        var market = position.Market;
        if (data is null
            || market is null
            || string.IsNullOrWhiteSpace(data.DealId)
            || string.IsNullOrWhiteSpace(data.Direction)
            || data.Size is null or <= 0
            || string.IsNullOrWhiteSpace(data.Currency)
            || string.IsNullOrWhiteSpace(market.Epic)
            || string.IsNullOrWhiteSpace(market.InstrumentName)
            || string.IsNullOrWhiteSpace(market.MarketStatus))
        {
            return null;
        }

        return new OpenPosition(
            data.DealId,
            market.Epic,
            market.InstrumentName,
            market.Expiry,
            data.Direction,
            data.Size.Value,
            data.Level,
            data.Currency,
            data.StopLevel,
            data.LimitLevel,
            data.CreatedDate,
            market.MarketStatus,
            market.Bid,
            market.Offer);
    }

    private static IReadOnlyList<WorkingOrder>? ParseWorkingOrders(string body)
    {
        var response = JsonSerializer.Deserialize<IgWorkingOrdersResponse>(body, JsonOptions);
        if (response?.WorkingOrders is null)
        {
            return null;
        }

        var orders = new List<WorkingOrder>(response.WorkingOrders.Count);
        foreach (var order in response.WorkingOrders)
        {
            var data = order.WorkingOrderData;
            var market = order.MarketData;
            if (data is null
                || market is null
                || string.IsNullOrWhiteSpace(data.DealId)
                || string.IsNullOrWhiteSpace(data.Direction)
                || string.IsNullOrWhiteSpace(data.Epic)
                || data.OrderSize is null or <= 0
                || data.OrderLevel is null
                || string.IsNullOrWhiteSpace(data.CurrencyCode)
                || string.IsNullOrWhiteSpace(data.TimeInForce)
                || string.IsNullOrWhiteSpace(data.OrderType)
                || string.IsNullOrWhiteSpace(market.InstrumentName)
                || string.IsNullOrWhiteSpace(market.MarketStatus))
            {
                return null;
            }

            orders.Add(new WorkingOrder(
                data.DealId,
                data.Epic,
                market.InstrumentName,
                data.Direction,
                data.OrderSize.Value,
                data.OrderLevel.Value,
                market.Expiry,
                data.CurrencyCode,
                data.TimeInForce,
                data.GoodTillDate,
                data.CreatedDate,
                data.GuaranteedStop,
                data.OrderType,
                data.StopDistance,
                data.LimitDistance,
                market.MarketStatus,
                market.Bid,
                market.Offer));
        }

        return orders;
    }

    private static DealConfirmation? ParseConfirmation(string body)
    {
        var response = JsonSerializer.Deserialize<IgConfirmationResponse>(body, JsonOptions);
        if (response is null
            || string.IsNullOrWhiteSpace(response.DealReference)
            || string.IsNullOrWhiteSpace(response.Status))
        {
            return null;
        }

        var affectedDeals = new List<AffectedDeal>();
        foreach (var deal in response.AffectedDeals ?? [])
        {
            if (string.IsNullOrWhiteSpace(deal.DealId) || string.IsNullOrWhiteSpace(deal.Status))
            {
                return null;
            }

            affectedDeals.Add(new AffectedDeal(deal.DealId, deal.Status));
        }

        return new DealConfirmation(
            response.DealReference,
            response.Status,
            response.Reason,
            response.DealStatus,
            response.Epic,
            response.DealId,
            response.Level,
            response.Size,
            response.Direction,
            response.StopLevel,
            response.LimitLevel,
            affectedDeals);
    }

    private static bool IsValidPositionCreateRequest(PositionCreateRequest? request) =>
        request is not null
        && IsValidIdentifier(request.Epic)
        && !string.IsNullOrWhiteSpace(request.Expiry)
        && IsValidDirection(request.Direction)
        && request.Size > 0
        && !string.IsNullOrWhiteSpace(request.OrderType)
        && !string.IsNullOrWhiteSpace(request.CurrencyCode)
        && (!request.TrailingStop || request.TrailingStopIncrement is > 0);

    private static bool IsValidPositionCloseRequest(PositionCloseRequest? request) =>
        request is not null
        && IsValidIdentifier(request.DealId)
        && IsValidIdentifier(request.Epic)
        && !string.IsNullOrWhiteSpace(request.Expiry)
        && IsValidDirection(request.Direction)
        && request.Size > 0
        && !string.IsNullOrWhiteSpace(request.OrderType)
        && !string.IsNullOrWhiteSpace(request.TimeInForce);

    private static bool IsValidWorkingOrderCreateRequest(WorkingOrderCreateRequest? request) =>
        request is not null
        && IsValidIdentifier(request.Epic)
        && !string.IsNullOrWhiteSpace(request.Expiry)
        && IsValidDirection(request.Direction)
        && request.Size > 0
        && request.Level > 0
        && !string.IsNullOrWhiteSpace(request.OrderType)
        && !string.IsNullOrWhiteSpace(request.CurrencyCode)
        && !string.IsNullOrWhiteSpace(request.TimeInForce)
        && !string.IsNullOrWhiteSpace(request.GoodTillDate);

    private static bool IsValidDirection(string? direction) =>
        string.Equals(direction, "BUY", StringComparison.OrdinalIgnoreCase)
        || string.Equals(direction, "SELL", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && !value.Contains('/');

    private static bool IsIndeterminate(TradingFailureCategory category) =>
        category is TradingFailureCategory.Unavailable
            or TradingFailureCategory.Timeout
            or TradingFailureCategory.MalformedProviderData;

    private static TradingFailureCategory MapFailure(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => TradingFailureCategory.UnsupportedEnvironment,
            IgRestGatewayClient.FailureCategory.Unauthorized => TradingFailureCategory.Unauthorized,
            IgRestGatewayClient.FailureCategory.RateLimited => TradingFailureCategory.RateLimited,
            IgRestGatewayClient.FailureCategory.Unavailable => TradingFailureCategory.Unavailable,
            IgRestGatewayClient.FailureCategory.Timeout => TradingFailureCategory.Timeout,
            IgRestGatewayClient.FailureCategory.MalformedProviderData => TradingFailureCategory.MalformedProviderData,
            _ => TradingFailureCategory.Rejected
        };

    private static TradingFailureCategory MapFailure(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => TradingFailureCategory.Unauthorized,
            HttpStatusCode.TooManyRequests => TradingFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => TradingFailureCategory.Timeout,
            >= HttpStatusCode.InternalServerError => TradingFailureCategory.Unavailable,
            >= HttpStatusCode.BadRequest => TradingFailureCategory.Rejected,
            _ => TradingFailureCategory.Unavailable
        };

    private static string SafeReason(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => "The applied IG Demo account is unavailable or does not match the requested account.",
            IgRestGatewayClient.FailureCategory.Unauthorized => "IG trading request was unauthorized.",
            IgRestGatewayClient.FailureCategory.RateLimited => "IG trading request was rate limited.",
            IgRestGatewayClient.FailureCategory.Unavailable => "IG trading service is unavailable; the operation may have completed.",
            IgRestGatewayClient.FailureCategory.Timeout => "IG trading request timed out; the operation may have completed.",
            IgRestGatewayClient.FailureCategory.MalformedProviderData => "IG trading response was malformed.",
            _ => "IG trading request was rejected."
        };

    private static string SafeReason(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "IG trading request was unauthorized.",
            HttpStatusCode.TooManyRequests => "IG trading request was rate limited.",
            HttpStatusCode.RequestTimeout => "IG trading request timed out; the operation may have completed.",
            >= HttpStatusCode.InternalServerError => "IG trading service is unavailable; the operation may have completed.",
            _ => "IG trading request was rejected."
        };

    private static TradingGatewayOutcome<T> Failure<T>(TradingFailureCategory category, string reason) =>
        new TradingGatewayOutcome<T>.Failed(category, reason);

    private static TradingGatewayOutcome<TradingMutationAcknowledgement> Indeterminate(
        TradingFailureCategory category,
        string reason) =>
        new TradingGatewayOutcome<TradingMutationAcknowledgement>.Indeterminate(category, reason);

    private sealed record IgPositionsResponse(
        [property: JsonPropertyName("positions")] List<IgOpenPosition>? Positions);

    private sealed record IgOpenPosition(
        [property: JsonPropertyName("position")] IgPositionData? Position,
        [property: JsonPropertyName("market")] IgPositionMarket? Market);

    private sealed record IgPositionData(
        [property: JsonPropertyName("createdDate")] string? CreatedDate,
        [property: JsonPropertyName("dealId")] string? DealId,
        [property: JsonPropertyName("size")] decimal? Size,
        [property: JsonPropertyName("direction")] string? Direction,
        [property: JsonPropertyName("limitLevel")] decimal? LimitLevel,
        [property: JsonPropertyName("level")] decimal? Level,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("stopLevel")] decimal? StopLevel);

    private sealed record IgPositionMarket(
        [property: JsonPropertyName("instrumentName")] string? InstrumentName,
        [property: JsonPropertyName("expiry")] string? Expiry,
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("marketStatus")] string? MarketStatus,
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("offer")] decimal? Offer);

    private sealed record IgWorkingOrdersResponse(
        [property: JsonPropertyName("workingOrders")] List<IgWorkingOrder>? WorkingOrders);

    private sealed record IgWorkingOrder(
        [property: JsonPropertyName("workingOrderData")] IgWorkingOrderData? WorkingOrderData,
        [property: JsonPropertyName("marketData")] IgWorkingOrderMarket? MarketData);

    private sealed record IgWorkingOrderData(
        [property: JsonPropertyName("dealId")] string? DealId,
        [property: JsonPropertyName("direction")] string? Direction,
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("orderSize")] decimal? OrderSize,
        [property: JsonPropertyName("orderLevel")] decimal? OrderLevel,
        [property: JsonPropertyName("timeInForce")] string? TimeInForce,
        [property: JsonPropertyName("goodTillDate")] string? GoodTillDate,
        [property: JsonPropertyName("createdDate")] string? CreatedDate,
        [property: JsonPropertyName("guaranteedStop")] bool GuaranteedStop,
        [property: JsonPropertyName("orderType")] string? OrderType,
        [property: JsonPropertyName("stopDistance")] decimal? StopDistance,
        [property: JsonPropertyName("limitDistance")] decimal? LimitDistance,
        [property: JsonPropertyName("currencyCode")] string? CurrencyCode);

    private sealed record IgWorkingOrderMarket(
        [property: JsonPropertyName("instrumentName")] string? InstrumentName,
        [property: JsonPropertyName("expiry")] string? Expiry,
        [property: JsonPropertyName("marketStatus")] string? MarketStatus,
        [property: JsonPropertyName("bid")] decimal? Bid,
        [property: JsonPropertyName("offer")] decimal? Offer);

    private sealed record IgConfirmationResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("dealStatus")] string? DealStatus,
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("dealReference")] string? DealReference,
        [property: JsonPropertyName("dealId")] string? DealId,
        [property: JsonPropertyName("affectedDeals")] List<IgAffectedDeal>? AffectedDeals,
        [property: JsonPropertyName("level")] decimal? Level,
        [property: JsonPropertyName("size")] decimal? Size,
        [property: JsonPropertyName("direction")] string? Direction,
        [property: JsonPropertyName("stopLevel")] decimal? StopLevel,
        [property: JsonPropertyName("limitLevel")] decimal? LimitLevel);

    private sealed record IgAffectedDeal(
        [property: JsonPropertyName("dealId")] string? DealId,
        [property: JsonPropertyName("status")] string? Status);

    private sealed record IgDealReferenceResponse(
        [property: JsonPropertyName("dealReference")] string? DealReference);

    private sealed record IgPositionCreateBody(
        [property: JsonPropertyName("epic")] string Epic,
        [property: JsonPropertyName("expiry")] string Expiry,
        [property: JsonPropertyName("direction")] string Direction,
        [property: JsonPropertyName("size")] decimal Size,
        [property: JsonPropertyName("level")] decimal? Level,
        [property: JsonPropertyName("orderType")] string OrderType,
        [property: JsonPropertyName("guaranteedStop")] bool GuaranteedStop,
        [property: JsonPropertyName("stopLevel")] decimal? StopLevel,
        [property: JsonPropertyName("stopDistance")] decimal? StopDistance,
        [property: JsonPropertyName("trailingStop")] bool TrailingStop,
        [property: JsonPropertyName("trailingStopIncrement")] decimal? TrailingStopIncrement,
        [property: JsonPropertyName("forceOpen")] bool ForceOpen,
        [property: JsonPropertyName("limitLevel")] decimal? LimitLevel,
        [property: JsonPropertyName("limitDistance")] decimal? LimitDistance,
        [property: JsonPropertyName("quoteId")] string? QuoteId,
        [property: JsonPropertyName("currencyCode")] string CurrencyCode)
    {
        internal IgPositionCreateBody(PositionCreateRequest request)
            : this(
                request.Epic,
                request.Expiry,
                request.Direction,
                request.Size,
                request.Level,
                request.OrderType,
                request.GuaranteedStop,
                request.StopLevel,
                request.StopDistance,
                request.TrailingStop,
                request.TrailingStopIncrement,
                request.ForceOpen,
                request.LimitLevel,
                request.LimitDistance,
                request.QuoteId,
                request.CurrencyCode)
        {
        }
    }

    private sealed record IgPositionAmendmentBody(
        [property: JsonPropertyName("stopLevel")] decimal? StopLevel,
        [property: JsonPropertyName("limitLevel")] decimal? LimitLevel,
        [property: JsonPropertyName("trailingStop")] bool TrailingStop,
        [property: JsonPropertyName("trailingStopDistance")] decimal? TrailingStopDistance,
        [property: JsonPropertyName("trailingStopIncrement")] decimal? TrailingStopIncrement)
    {
        internal IgPositionAmendmentBody(PositionAmendmentRequest request)
            : this(
                request.StopLevel,
                request.LimitLevel,
                request.TrailingStop,
                request.TrailingStopDistance,
                request.TrailingStopIncrement)
        {
        }
    }

    private sealed record IgPositionCloseBody(
        [property: JsonPropertyName("dealId")] string DealId,
        [property: JsonPropertyName("epic")] string Epic,
        [property: JsonPropertyName("expiry")] string Expiry,
        [property: JsonPropertyName("direction")] string Direction,
        [property: JsonPropertyName("size")] decimal Size,
        [property: JsonPropertyName("level")] decimal? Level,
        [property: JsonPropertyName("orderType")] string OrderType,
        [property: JsonPropertyName("timeInForce")] string TimeInForce,
        [property: JsonPropertyName("quoteId")] string? QuoteId)
    {
        internal IgPositionCloseBody(PositionCloseRequest request)
            : this(
                request.DealId,
                request.Epic,
                request.Expiry,
                request.Direction,
                request.Size,
                request.Level,
                request.OrderType,
                request.TimeInForce,
                request.QuoteId)
        {
        }
    }

    private sealed record IgWorkingOrderCreateBody(
        [property: JsonPropertyName("epic")] string Epic,
        [property: JsonPropertyName("expiry")] string Expiry,
        [property: JsonPropertyName("direction")] string Direction,
        [property: JsonPropertyName("size")] decimal Size,
        [property: JsonPropertyName("level")] decimal Level,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("currencyCode")] string CurrencyCode,
        [property: JsonPropertyName("timeInForce")] string TimeInForce,
        [property: JsonPropertyName("goodTillDate")] string GoodTillDate,
        [property: JsonPropertyName("guaranteedStop")] bool GuaranteedStop,
        [property: JsonPropertyName("stopDistance")] decimal? StopDistance,
        [property: JsonPropertyName("limitDistance")] decimal? LimitDistance)
    {
        internal IgWorkingOrderCreateBody(WorkingOrderCreateRequest request)
            : this(
                request.Epic,
                request.Expiry,
                request.Direction,
                request.Size,
                request.Level,
                request.OrderType,
                request.CurrencyCode,
                request.TimeInForce,
                request.GoodTillDate,
                request.GuaranteedStop,
                request.StopDistance,
                request.LimitDistance)
        {
        }
    }

    private sealed record IgWorkingOrderAmendmentBody(
        [property: JsonPropertyName("timeInForce")] string TimeInForce,
        [property: JsonPropertyName("goodTillDate")] string GoodTillDate,
        [property: JsonPropertyName("stopDistance")] decimal? StopDistance,
        [property: JsonPropertyName("limitDistance")] decimal? LimitDistance,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("level")] decimal Level)
    {
        internal IgWorkingOrderAmendmentBody(WorkingOrderAmendmentRequest request)
            : this(
                request.TimeInForce,
                request.GoodTillDate,
                request.StopDistance,
                request.LimitDistance,
                request.OrderType,
                request.Level)
        {
        }
    }

    private sealed record IgEmptyBody;
}
