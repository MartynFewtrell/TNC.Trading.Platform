namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class InstrumentCollectionCategoryAttemptEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public DateOnly TradingDay { get; set; }
    public int ScheduledSlot { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string State { get; set; } = "Pending";
    public long LeaseFence { get; set; }
    public string? SafeError { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
