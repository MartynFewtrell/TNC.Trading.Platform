namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record PositionCloseRequest(
    string DealId,
    string Epic,
    string Expiry,
    string Direction,
    decimal Size,
    string OrderType,
    string TimeInForce,
    string? QuoteId = null,
    decimal? Level = null);
