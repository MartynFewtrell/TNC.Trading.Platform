namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDataFullRunSlotCoverageEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public DateOnly TradingDay { get; set; }
    public long ScheduleRevision { get; set; }
    public int ScheduledSlot { get; set; }
    public Guid RunId { get; set; }
    public string CoverageKind { get; set; } = string.Empty;
    public DateTimeOffset CoveredAtUtc { get; set; }
}
