namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryInstrumentCollectionRunEntity
{
    public Guid CollectionId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string EndpointProfile { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public long CategorySnapshotRevision { get; set; }
    public long SnapshotVersion { get; set; }
    public DateOnly TradingDay { get; set; }
    public int ScheduledSlot { get; set; }
    public int EffectiveUpdatesPerDay { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public int ProviderTotalPages { get; set; }
    public int ProviderTotalResults { get; set; }
    public int ResultCount { get; set; }
    public string QualityStatus { get; set; } = string.Empty;
    public int MissingOptionalValueCount { get; set; }
    public bool IsComplete { get; set; }
    public ICollection<MarketCategoryInstrumentObservationEntity> Observations { get; set; } = [];
}
