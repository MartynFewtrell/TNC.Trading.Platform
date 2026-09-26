namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailRunMembershipEntity
{
    public Guid RunId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string Epic { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public MarketDetailRunTargetEntity Target { get; set; } = null!;
    public MarketDetailRunSourceEntity Source { get; set; } = null!;
}
