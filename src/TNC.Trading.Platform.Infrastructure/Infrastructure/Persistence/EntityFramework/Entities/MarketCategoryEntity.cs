namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketCategoryEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool NonTradeable { get; set; }
}
