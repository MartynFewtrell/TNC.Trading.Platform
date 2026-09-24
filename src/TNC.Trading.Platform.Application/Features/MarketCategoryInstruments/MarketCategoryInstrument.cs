namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A normalized instrument value saved from a complete category observation.</summary>
internal sealed record MarketCategoryInstrument(
    string Epic,
    string InstrumentName,
    string? InstrumentType,
    string? UnderlyingName,
    string? Expiry,
    decimal? LotSize,
    bool? OtcTradeable,
    decimal? ScalingFactor,
    long? ExpiryTimestamp,
    string? MarketStatus,
    int? DelayTime,
    decimal? Bid,
    decimal? Offer,
    decimal? High,
    decimal? Low,
    decimal? NetChange,
    decimal? PercentageChange,
    string? UpdateTime,
    long? Popularity);
