using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal sealed record MarketDataFailedItemRetry(
    Guid SourceRunId,
    BrokerEnvironmentKind Environment,
    Guid AppliedBrokerEnvironmentId,
    string EndpointProfile,
    DateOnly TradingDay,
    int DetailScheduledSlot,
    DateTimeOffset DueAtUtc,
    DateTimeOffset WindowEndUtc,
    long ScheduleRevision,
    int EffectiveUpdatesPerDay,
    long CollectionConfigurationVersion,
    long InterestRevision,
    IReadOnlyList<string> SelectedCategoryCodes);
