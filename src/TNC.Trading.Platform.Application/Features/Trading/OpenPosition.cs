namespace TNC.Trading.Platform.Application.Features.Trading;

internal sealed record OpenPosition(
    string DealId,
    string Epic,
    string InstrumentName,
    string? Expiry,
    string Direction,
    decimal Size,
    decimal? OpenLevel,
    string Currency,
    decimal? StopLevel,
    decimal? LimitLevel,
    string? CreatedDate,
    string MarketStatus,
    decimal? Bid,
    decimal? Offer);
