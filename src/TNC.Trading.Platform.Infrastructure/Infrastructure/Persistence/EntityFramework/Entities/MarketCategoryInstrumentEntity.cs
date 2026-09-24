namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryInstrumentEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string Epic { get; set; } = string.Empty;
    public long SnapshotVersion { get; set; }
    public Guid CollectionId { get; set; }
    public string InstrumentName { get; set; } = string.Empty;
    public string? InstrumentType { get; set; }
    public string? UnderlyingName { get; set; }
    public string? Expiry { get; set; }
    public decimal? LotSize { get; set; }
    public bool? OtcTradeable { get; set; }
    public decimal? ScalingFactor { get; set; }
    public long? ExpiryTimestamp { get; set; }
    public string? MarketStatus { get; set; }
    public int? DelayTime { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Offer { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? NetChange { get; set; }
    public decimal? PercentageChange { get; set; }
    public string? UpdateTime { get; set; }
    public long? Popularity { get; set; }
}
