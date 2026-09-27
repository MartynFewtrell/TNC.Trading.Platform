using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDataRuns;

internal sealed record MarketDataFullRunAdmissionRequest(
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
    IReadOnlyList<MarketDataFullRunSlotIdentity> CoveredSlots,
    Guid LeaseOwner,
    TimeSpan LeaseDuration,
    bool ResumeOnly = false,
    Guid? RetrySourceRunId = null,
    int? DetailScheduledSlot = null);
