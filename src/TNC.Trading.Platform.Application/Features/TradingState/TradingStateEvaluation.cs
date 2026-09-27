using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.TradingState;

internal sealed record TradingStateEvaluation(
    bool TradingWindowOpen,
    bool CanTrade,
    bool CanStartMarketDataUpdate,
    Guid? AppliedBrokerEnvironmentId,
    long ScheduleRevision,
    long CollectionConfigurationVersion,
    DateOnly? TradingDay,
    DateTimeOffset? NextWindowOpeningUtc,
    DateTimeOffset? NextWindowClosingUtc,
    DateTimeOffset? NextScheduledStartUtc,
    IReadOnlyList<TradingStateBlockReason> TradeBlockReasons,
    IReadOnlyList<TradingStateBlockReason> MarketDataBlockReasons);
