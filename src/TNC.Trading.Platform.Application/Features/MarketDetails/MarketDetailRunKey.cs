using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailRunKey(
    BrokerEnvironmentKind Environment,
    DateOnly TradingDay,
    int SlotIndex);
