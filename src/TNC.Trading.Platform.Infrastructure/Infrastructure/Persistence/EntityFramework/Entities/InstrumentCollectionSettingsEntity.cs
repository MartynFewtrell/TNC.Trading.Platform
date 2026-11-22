namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class InstrumentCollectionSettingsEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public int CurrentUpdatesPerDay { get; set; }
    public int? PendingUpdatesPerDay { get; set; }
    public DateOnly? PendingEffectiveTradingDay { get; set; }
    public int? ApprovedNonTradingDailyRequestAllowance { get; set; }
    public int LeadInMinutes { get; set; } = 15;
    public int CategoryPageSize { get; set; } = 25;
    public int RequestSpacingSeconds { get; set; } = 5;
    public int CategoryPauseSeconds { get; set; } = 60;
    public long ConfigurationVersion { get; set; } = 1;
}
