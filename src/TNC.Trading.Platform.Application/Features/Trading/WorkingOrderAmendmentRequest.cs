namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record WorkingOrderAmendmentRequest(
    string TimeInForce,
    string GoodTillDate,
    string OrderType,
    decimal Level,
    decimal? StopDistance = null,
    decimal? LimitDistance = null);
