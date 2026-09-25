namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class InstrumentCollectionCycleStateEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public DateOnly TradingDay { get; set; }
    public int ScheduledSlot { get; set; }
    public long ScheduleRevision { get; set; }
    public string CategoryPrerequisite { get; set; } = "Pending";
    public string? CategoryPrerequisiteSafeError { get; set; }
    public int CategoryPrerequisiteAttempts { get; set; }
    public long CategoryPrerequisiteLeaseFence { get; set; }
    public string Outcome { get; set; } = "Pending";
    public Guid? LeaseOwner { get; set; }
    public long LeaseFence { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public int UsedRequestBudget { get; set; }
}
