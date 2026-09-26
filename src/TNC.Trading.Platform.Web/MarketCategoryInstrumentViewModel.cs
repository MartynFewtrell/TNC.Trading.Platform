namespace TNC.Trading.Platform.Web;

internal sealed record MarketCategoryInstrumentViewModel(
    string Epic,
    string Name,
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
    long? Popularity,
    MarketDetailAvailabilityViewModel? MarketDetails = null);
