namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDataFullRunCategoryEntity
{
    public Guid RunId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
}
