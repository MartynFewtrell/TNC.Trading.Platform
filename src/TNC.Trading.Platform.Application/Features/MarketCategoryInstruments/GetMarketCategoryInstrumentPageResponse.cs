using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal enum MarketCategoryInstrumentPageReadStatus
{
    Page,
    CategoryNotFound,
    NeverCollected,
    StaleCursor,
    AppliedEnvironmentUnavailable
}

internal sealed record GetMarketCategoryInstrumentPageResponse(
    MarketCategoryInstrumentPageReadStatus Status,
    BrokerEnvironmentKind? BrokerEnvironment,
    string CategoryCode,
    MarketCategoryInstrumentSnapshotPage? Page);
