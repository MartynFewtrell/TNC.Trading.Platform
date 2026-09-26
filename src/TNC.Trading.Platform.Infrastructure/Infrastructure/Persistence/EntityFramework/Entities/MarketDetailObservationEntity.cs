namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailObservationEntity
{
    public Guid RunId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string Epic { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public string SourceEndpoint { get; set; } = string.Empty;
    public int SourceVersion { get; set; }
    public int DetailSchemaVersion { get; set; }
    public string? ProviderUpdateTimeText { get; set; }
    public string InstrumentName { get; set; } = string.Empty;
    public string? InstrumentType { get; set; }
    public string? MarketId { get; set; }
    public string? Expiry { get; set; }
    public string? InstrumentUnit { get; set; }
    public decimal? LotSize { get; set; }
    public bool? ForceOpenAllowed { get; set; }
    public bool? StopsLimitsAllowed { get; set; }
    public bool? ControlledRiskAllowed { get; set; }
    public bool? StreamingPricesAvailable { get; set; }
    public decimal? MarginFactor { get; set; }
    public string? MarginFactorUnit { get; set; }
    public string InstrumentJson { get; set; } = string.Empty;
    public string DealingRulesJson { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string MarketStatus { get; set; } = string.Empty;
    public decimal? DelayTime { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Offer { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? NetChange { get; set; }
    public decimal? PercentageChange { get; set; }
    public decimal? BinaryOdds { get; set; }
    public decimal? DecimalPlacesFactor { get; set; }
    public decimal? ScalingFactor { get; set; }
    public decimal? ControlledRiskExtraSpread { get; set; }
    public decimal? MinStepDistance { get; set; }
    public string? MinStepDistanceUnit { get; set; }
    public decimal? MinDealSize { get; set; }
    public string? MinDealSizeUnit { get; set; }
    public decimal? MinControlledRiskStopDistance { get; set; }
    public string? MinControlledRiskStopDistanceUnit { get; set; }
    public decimal? MinNormalStopOrLimitDistance { get; set; }
    public string? MinNormalStopOrLimitDistanceUnit { get; set; }
    public decimal? MaxStopOrLimitDistance { get; set; }
    public string? MaxStopOrLimitDistanceUnit { get; set; }
    public decimal? ControlledRiskSpacing { get; set; }
    public string? ControlledRiskSpacingUnit { get; set; }
    public string? MarketOrderPreference { get; set; }
    public string? TrailingStopsPreference { get; set; }
    public MarketDetailCollectionRunEntity Run { get; set; } = null!;
    public MarketDetailRunTargetEntity Target { get; set; } = null!;
}
