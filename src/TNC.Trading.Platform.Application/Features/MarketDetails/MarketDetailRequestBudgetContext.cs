using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailRequestBudgetContext(
    Guid RunId,
    BrokerEnvironmentKind Environment,
    DateOnly TradingDay,
    int SlotIndex,
    Guid LeaseOwner,
    long LeaseFence,
    long ScheduleRevision,
    int EffectiveUpdatesPerDay,
    string AppliedEndpointProfile,
    DateTimeOffset WindowEndUtc);
