namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailCollectionRunEntity
{
    public Guid RunId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string EndpointProfile { get; set; } = string.Empty;
    public DateOnly TradingDay { get; set; }
    public int ScheduledSlot { get; set; }
    public long CatalogueRevision { get; set; }
    public long InterestRevision { get; set; }
    public long ScheduleRevision { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ExpectedCount { get; set; }
    public int CompletedCount { get; set; }
    public int ExcludedCount { get; set; }
    public bool IsUniverseStaged { get; set; }
    public DateTimeOffset WindowEndUtc { get; set; }
    public Guid? LeaseOwner { get; set; }
    public long LeaseFence { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? SafeReasonCode { get; set; }
    public byte[] ConcurrencyToken { get; set; } = [];
    public ICollection<MarketDetailRunSourceEntity> Sources { get; set; } = [];
    public ICollection<MarketDetailRunTargetEntity> Targets { get; set; } = [];
    public ICollection<MarketDetailObservationEntity> Observations { get; set; } = [];
}
