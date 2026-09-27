namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDataFullRunIntentEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public long CollectionConfigurationVersion { get; set; }
    public long InterestRevision { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
