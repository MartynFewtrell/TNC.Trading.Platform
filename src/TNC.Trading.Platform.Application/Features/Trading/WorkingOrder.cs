namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record WorkingOrder(
    string DealId,
    string Epic,
    string InstrumentName,
    string Direction,
    decimal Size,
    decimal Level,
    string? Expiry,
    string CurrencyCode,
    string TimeInForce,
    string? GoodTillDate,
    string? CreatedDate,
    bool GuaranteedStop,
    string OrderType,
    decimal? StopDistance,
    decimal? LimitDistance,
    string MarketStatus,
    decimal? Bid,
    decimal? Offer);
