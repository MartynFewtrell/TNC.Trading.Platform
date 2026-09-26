namespace TNC.Trading.Platform.Web;

internal sealed record MarketDetailViewModel(
    string State,
    string CategoryCode,
    string Epic,
    long? ListingSnapshotVersion,
    DateTimeOffset? ListingRetrievedAtUtc,
    MarketDetailViewModel.CoverageViewModel Coverage,
    MarketDetailViewModel.ObservationViewModel? SavedObservation)
{
    internal sealed record CoverageViewModel(
        bool IsFollowed,
        string State,
        int? ExpectedCount,
        int? CompletedCount,
        int? ExcludedCount,
        int? OutstandingCount,
        DateTimeOffset? LastCompleteAtUtc,
        DateTimeOffset? NextScheduledCheckUtc,
        string? SafeFailureCode);

    internal sealed record ObservationViewModel(
        DateTimeOffset RetrievedAtUtc,
        string Source,
        string SourceEndpoint,
        int SourceVersion,
        string? ProviderUpdateTimeText,
        InstrumentViewModel Instrument,
        DealingRulesViewModel DealingRules,
        MarketSnapshotViewModel Snapshot);

    internal sealed record InstrumentViewModel(
        string Epic,
        string Expiry,
        string Name,
        string? MarketId,
        string Type,
        string Unit,
        decimal LotSize,
        bool? ForceOpenAllowed,
        bool? StopsLimitsAllowed,
        bool? ControlledRiskAllowed,
        bool? StreamingPricesAvailable,
        IReadOnlyList<CurrencyViewModel> Currencies,
        IReadOnlyList<MarginDepositBandViewModel> MarginDepositBands,
        decimal? MarginFactor,
        string? MarginFactorUnit,
        QuantityViewModel SlippageFactor,
        QuantityViewModel LimitedRiskPremium,
        QuantityViewModel SprintMarketsMinimumExpiryTime,
        QuantityViewModel SprintMarketsMaximumExpiryTime,
        string? OpeningHoursJson,
        string? ExpiryDetailsJson,
        string? RolloverDetailsJson,
        string? NewsCode,
        string? ChartCode,
        string? Country,
        string? ValueOfOnePip,
        string? OnePipMeans,
        string? ContractSize,
        IReadOnlyList<string> SpecialInfo);

    internal sealed record CurrencyViewModel(
        string Code,
        string Symbol,
        decimal? BaseExchangeRate,
        decimal? ExchangeRate,
        bool IsDefault);

    internal sealed record MarginDepositBandViewModel(
        decimal Minimum,
        QuantityViewModel Maximum,
        decimal Margin,
        string Currency);

    internal sealed record DealingRulesViewModel(
        QuantityViewModel ControlledRiskSpacing,
        QuantityViewModel MaxStopOrLimitDistance,
        QuantityViewModel MinControlledRiskStopDistance,
        QuantityViewModel MinDealSize,
        QuantityViewModel MinNormalStopOrLimitDistance,
        QuantityViewModel MinStepDistance,
        string MarketOrderPreference,
        string TrailingStopsPreference);

    internal sealed record MarketSnapshotViewModel(
        string MarketStatus,
        QuantityViewModel NetChange,
        QuantityViewModel PercentageChange,
        string? UpdateTimeText,
        QuantityViewModel DelayTime,
        QuantityViewModel Bid,
        QuantityViewModel Offer,
        QuantityViewModel High,
        QuantityViewModel Low,
        QuantityViewModel BinaryOdds,
        QuantityViewModel DecimalPlacesFactor,
        QuantityViewModel ScalingFactor,
        QuantityViewModel ControlledRiskExtraSpread);

    internal sealed record QuantityViewModel(
        string Presence,
        decimal? Value,
        string? Unit);
}
