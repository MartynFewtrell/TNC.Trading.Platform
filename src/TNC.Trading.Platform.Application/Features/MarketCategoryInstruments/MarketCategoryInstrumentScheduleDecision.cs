namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A due local schedule slot or an explicit reason collection must not run.</summary>
internal sealed record MarketCategoryInstrumentScheduleDecision(
    bool IsDue,
    MarketCategoryInstrumentScheduleBlockReason? BlockReason,
    DateOnly? TradingDay,
    int? SlotIndex,
    int? EffectiveUpdatesPerDay,
    string? ScheduleIdentity,
    IReadOnlyList<int> MissedSlotIndexes);
