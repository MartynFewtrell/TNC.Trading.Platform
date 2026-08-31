namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class AccountPreferencesDesiredStateAuditEntity
{
    public Guid AccountPreferencesDesiredStateAuditId { get; set; }
    public Guid AccountPreferencesCurrentStateId { get; set; }
    public string PlatformEnvironment { get; set; } = null!;
    public string BrokerEnvironment { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public bool? PreviousValue { get; set; }
    public bool NewValue { get; set; }
    public long PreviousRevision { get; set; }
    public long NewRevision { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Actor { get; set; } = null!;
    public string ChangeType { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
}