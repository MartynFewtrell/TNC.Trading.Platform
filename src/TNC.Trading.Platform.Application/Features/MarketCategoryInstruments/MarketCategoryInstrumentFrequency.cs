namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Configured update frequency, including a pending value retained for persisted-contract compatibility.</summary>
internal sealed record MarketCategoryInstrumentFrequency(
    int CurrentUpdatesPerDay,
    int? PendingUpdatesPerDay,
    DateOnly? PendingEffectiveTradingDay,
    int? ApprovedNonTradingDailyRequestAllowance = null,
    int LeadInMinutes = 15,
    long ConfigurationVersion = 1)
{
    /// <summary>Returns the authoritative current count; persisted legacy pending values are not promoted by policy.</summary>
    public int ForTradingDay(DateOnly tradingDay) => CurrentUpdatesPerDay;
}
