using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Fenced identity for one environment-local-day-slot execution.</summary>
internal sealed record MarketCategoryInstrumentCycleLease(
    BrokerEnvironmentKind BrokerEnvironment,
    DateOnly TradingDay,
    int ScheduledSlot,
    int EffectiveUpdatesPerDay,
    long ScheduleRevision,
    Guid Owner,
    long Fence,
    DateTimeOffset WindowEndUtc,
    string EndpointProfile);
