namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDetailEligibilityEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string Epic { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EvidenceCode { get; set; }
    public DateTimeOffset? ExcludedAtUtc { get; set; }
    public DateTimeOffset? ReinstatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
