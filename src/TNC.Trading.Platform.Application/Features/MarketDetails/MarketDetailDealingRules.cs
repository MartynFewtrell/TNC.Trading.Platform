namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailDealingRules(
    MarketDetailQuantity ControlledRiskSpacing,
    MarketDetailQuantity MaxStopOrLimitDistance,
    MarketDetailQuantity MinControlledRiskStopDistance,
    MarketDetailQuantity MinDealSize,
    MarketDetailQuantity MinNormalStopOrLimitDistance,
    MarketDetailQuantity MinStepDistance,
    string MarketOrderPreference,
    string TrailingStopsPreference);
