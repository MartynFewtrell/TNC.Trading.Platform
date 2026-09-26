namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailRunSourceEntity
{
    public Guid RunId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public Guid ListingCollectionId { get; set; }
    public long ListingVersion { get; set; }
    public bool IsValidatedComplete { get; set; }
    public MarketDetailCollectionRunEntity Run { get; set; } = null!;
    public ICollection<MarketDetailRunMembershipEntity> Memberships { get; set; } = [];
}
