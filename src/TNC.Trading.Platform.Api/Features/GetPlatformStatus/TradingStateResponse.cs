namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record TradingStateResponse(
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
    IReadOnlyList<string> TradeBlockReasons,
    IReadOnlyList<string> MarketDataBlockReasons);
