namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailMarketSnapshot(
    string MarketStatus,
    MarketDetailQuantity NetChange,
    MarketDetailQuantity PercentageChange,
    string? UpdateTimeText,
    MarketDetailQuantity DelayTime,
    MarketDetailQuantity Bid,
    MarketDetailQuantity Offer,
    MarketDetailQuantity High,
    MarketDetailQuantity Low,
    MarketDetailQuantity BinaryOdds,
    MarketDetailQuantity DecimalPlacesFactor,
    MarketDetailQuantity ScalingFactor,
    MarketDetailQuantity ControlledRiskExtraSpread);
