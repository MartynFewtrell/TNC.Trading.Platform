using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;

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
    DateTimeOffset WindowEndUtc,
    MarketDataFullRunLease? FullRunLease = null);
