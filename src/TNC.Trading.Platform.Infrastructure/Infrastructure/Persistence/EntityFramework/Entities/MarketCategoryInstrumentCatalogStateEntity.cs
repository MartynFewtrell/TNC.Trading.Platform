namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryInstrumentCatalogStateEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public long SnapshotVersion { get; set; }
    public Guid CollectionId { get; set; }
    public DateTimeOffset LastRefreshedAtUtc { get; set; }
}
