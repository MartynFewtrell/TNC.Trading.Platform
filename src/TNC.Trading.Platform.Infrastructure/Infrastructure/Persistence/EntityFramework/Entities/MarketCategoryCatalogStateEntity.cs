namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryCatalogStateEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public DateTimeOffset LastRefreshedAtUtc { get; set; }
}
