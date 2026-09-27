namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal sealed record MarketDataFullRunSlotIdentity(
    DateOnly TradingDay,
    long ScheduleRevision,
    int ScheduledSlot);
