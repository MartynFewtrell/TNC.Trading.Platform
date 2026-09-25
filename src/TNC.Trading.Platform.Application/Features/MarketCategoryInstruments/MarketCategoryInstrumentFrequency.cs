namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Current and pending update frequency, with pending values effective on a local trading day.</summary>
internal sealed record MarketCategoryInstrumentFrequency(
    int CurrentUpdatesPerDay,
    int? PendingUpdatesPerDay,
    DateOnly? PendingEffectiveTradingDay,
    int? ApprovedNonTradingDailyRequestAllowance = null)
{
    public int ForTradingDay(DateOnly tradingDay) =>
        PendingUpdatesPerDay is not null
        && PendingEffectiveTradingDay is not null
        && tradingDay >= PendingEffectiveTradingDay
            ? PendingUpdatesPerDay.Value
            : CurrentUpdatesPerDay;
}
