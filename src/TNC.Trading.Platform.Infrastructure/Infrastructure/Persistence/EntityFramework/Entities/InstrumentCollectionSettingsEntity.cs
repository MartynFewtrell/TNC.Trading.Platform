namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class InstrumentCollectionSettingsEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public int CurrentUpdatesPerDay { get; set; }
    public int? PendingUpdatesPerDay { get; set; }
    public DateOnly? PendingEffectiveTradingDay { get; set; }
    public int? ApprovedNonTradingDailyRequestAllowance { get; set; }
}
