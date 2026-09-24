using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Identifies the local schedule slot and UTC observation time for one successful collection.</summary>
internal sealed record MarketCategoryInstrumentRunProvenance(
    Guid RunId,
    BrokerEnvironmentKind BrokerEnvironment,
    string AppliedEndpointProfile,
    string CategoryCode,
    long CategorySnapshotRevision,
    DateOnly TradingDay,
    int ScheduledSlot,
    int EffectiveUpdatesPerDay,
    DateTimeOffset RetrievedAtUtc,
    MarketCategoryInstrumentCollectionMetadata CollectionMetadata,
    MarketCategoryInstrumentDataQualityEvidence DataQualityEvidence,
    Guid LeaseOwner,
    long LeaseFence,
    DateTimeOffset? ScheduleWindowEndUtc = null);
