namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Last local-day slot observation used only to identify missed slots and prevent replay.</summary>
internal sealed record MarketCategoryInstrumentSlotProgress(
    DateOnly TradingDay,
    int SlotIndex,
    int UpdatesPerDay,
    string ScheduleIdentity);
