namespace TNC.Trading.Platform.Application.Features.MarketData;

internal sealed record MarketDetails(
    string Epic,
    string Name,
    string InstrumentType,
    string MarketStatus,
    decimal? Bid,
    decimal? Offer,
    decimal? High,
    decimal? Low,
    decimal? NetChange,
    decimal? PercentageChange,
    string? ProviderUpdateTime,
    int DelayTimeMinutes,
    int ScalingFactor);
