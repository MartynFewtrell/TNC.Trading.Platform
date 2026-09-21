namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentRetirementAuditEntity
{
    public long BrokerEnvironmentRetirementAuditId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string PurgeCountsJson { get; set; } = string.Empty;
    public string RetainedCountsJson { get; set; } = string.Empty;
}