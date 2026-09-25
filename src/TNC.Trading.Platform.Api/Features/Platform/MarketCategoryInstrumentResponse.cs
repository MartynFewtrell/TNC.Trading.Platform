namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Snapshot-only instrument values, including the provider's quoted local update text.</summary>
internal sealed record MarketCategoryInstrumentResponse(
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
    long? Popularity);
