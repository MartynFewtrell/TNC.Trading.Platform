using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal sealed record MarketDataFullRunLease(
    Guid RunId,
    BrokerEnvironmentKind Environment,
    Guid AppliedBrokerEnvironmentId,
    string EndpointProfile,
    DateOnly TradingDay,
    DateTimeOffset AdmittedAtUtc,
    DateTimeOffset WindowEndUtc,
    long ScheduleRevision,
    int EffectiveUpdatesPerDay,
    long CollectionConfigurationVersion,
    long InterestRevision,
    MarketDataFullRunTrigger Trigger,
    IReadOnlyList<string> SelectedCategoryCodes,
    Guid LeaseOwner,
    long LeaseFence,
    DateTimeOffset LeaseExpiresAtUtc,
    IReadOnlyList<MarketDataFullRunSlotIdentity>? CoveredSlots = null,
    int? DetailScheduledSlot = null);
