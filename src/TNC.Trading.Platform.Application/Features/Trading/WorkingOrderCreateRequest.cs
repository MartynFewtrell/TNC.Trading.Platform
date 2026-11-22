namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record WorkingOrderCreateRequest(
    string Epic,
    string Expiry,
    string Direction,
    decimal Size,
    decimal Level,
    string OrderType,
    string CurrencyCode,
    string TimeInForce,
    string GoodTillDate,
    bool GuaranteedStop,
    decimal? StopDistance = null,
    decimal? LimitDistance = null);
