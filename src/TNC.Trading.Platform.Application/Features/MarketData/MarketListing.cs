namespace TNC.Trading.Platform.Application.Features.MarketData;

internal sealed record MarketListing(
    string Epic,
    string InstrumentName,
    string InstrumentType,
    string? Expiry,
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
