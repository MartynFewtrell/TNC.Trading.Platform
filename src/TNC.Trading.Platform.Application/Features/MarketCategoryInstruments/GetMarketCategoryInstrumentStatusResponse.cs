using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed record GetMarketCategoryInstrumentStatusResponse(
    bool IsAvailable,
    BrokerEnvironmentKind? BrokerEnvironment,
    DateOnly? TradingDay,
    MarketCategoryInstrumentCollectionStatus? CollectionStatus,
    bool IsDue,
    int? CurrentSlot,
    DateTimeOffset? NextWakeUpUtc,
    string? PauseReason);
