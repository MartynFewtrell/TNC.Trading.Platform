namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryInterestStateEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public long Revision { get; set; }
}
