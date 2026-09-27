namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDataFullRunEntity
{
    public Guid RunId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string EndpointProfile { get; set; } = string.Empty;
    public DateOnly TradingDay { get; set; }
    public int? DetailScheduledSlot { get; set; }
    public DateTimeOffset AdmittedAtUtc { get; set; }
    public DateTimeOffset WindowEndUtc { get; set; }
    public long ScheduleRevision { get; set; }
    public int EffectiveUpdatesPerDay { get; set; }
    public long CollectionConfigurationVersion { get; set; }
    public long InterestRevision { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public string CurrentStage { get; set; } = "Categories";
    public string? Outcome { get; set; }
    public string? SafeReasonCode { get; set; }
    public Guid? LeaseOwner { get; set; }
    public long LeaseFence { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? FailedItemFollowUpDueAtUtc { get; set; }
    public Guid? FailedItemFollowUpRunId { get; set; }
    public DateTimeOffset? FailedItemFollowUpCancelledAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
