using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record CollectMarketDetailsRequest(
    BrokerEnvironmentKind AppliedEnvironment,
    DateOnly TradingDay,
    int SlotIndex,
    MarketDetailRevisions Revisions,
    string AppliedEndpointProfile,
    DateTimeOffset WindowEndUtc);
