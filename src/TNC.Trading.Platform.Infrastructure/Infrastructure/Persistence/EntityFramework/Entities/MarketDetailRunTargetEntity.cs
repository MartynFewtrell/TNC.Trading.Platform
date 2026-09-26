namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailRunTargetEntity
{
    public Guid RunId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string Epic { get; set; } = string.Empty;
    public string Status { get; set; } = "NotCollected";
    public int Attempts { get; set; }
    public string? SafeFailureCode { get; set; }
    public string? ExclusionEvidenceCode { get; set; }
    public DateTimeOffset? ExcludedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public MarketDetailCollectionRunEntity Run { get; set; } = null!;
    public ICollection<MarketDetailRunMembershipEntity> Memberships { get; set; } = [];
    public ICollection<MarketDetailObservationEntity> Observations { get; set; } = [];
}
