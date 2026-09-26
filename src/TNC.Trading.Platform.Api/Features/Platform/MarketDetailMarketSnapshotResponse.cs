namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Saved historical quote snapshot, not a live price stream.</summary>
internal sealed record MarketDetailMarketSnapshotResponse(
    string MarketStatus,
    MarketDetailQuantityResponse NetChange,
    MarketDetailQuantityResponse PercentageChange,
    string? UpdateTimeText,
    MarketDetailQuantityResponse DelayTime,
    MarketDetailQuantityResponse Bid,
    MarketDetailQuantityResponse Offer,
    MarketDetailQuantityResponse High,
    MarketDetailQuantityResponse Low,
    MarketDetailQuantityResponse BinaryOdds,
    MarketDetailQuantityResponse DecimalPlacesFactor,
    MarketDetailQuantityResponse ScalingFactor,
    MarketDetailQuantityResponse ControlledRiskExtraSpread);
