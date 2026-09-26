namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Saved dealing-rule distances with each rule's own nullable value and unit.</summary>
internal sealed record MarketDetailDealingRulesResponse(
    MarketDetailQuantityResponse ControlledRiskSpacing,
    MarketDetailQuantityResponse MaxStopOrLimitDistance,
    MarketDetailQuantityResponse MinControlledRiskStopDistance,
    MarketDetailQuantityResponse MinDealSize,
    MarketDetailQuantityResponse MinNormalStopOrLimitDistance,
    MarketDetailQuantityResponse MinStepDistance,
    string MarketOrderPreference,
    string TrailingStopsPreference);
