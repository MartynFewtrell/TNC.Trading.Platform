namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryInterestEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public DateTimeOffset SelectedAtUtc { get; set; }
}
