namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailCurrentEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string Epic { get; set; } = string.Empty;
    public Guid RunId { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
