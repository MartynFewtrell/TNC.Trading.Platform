using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed record RefreshMarketCategoriesRequest(
    MarketCategoryInstrumentCycleLease? ScheduledLease = null,
    CancellationToken ScheduleCancellationToken = default,
    MarketCategoryInstrumentRequestBudgetContext? ManualBudgetContext = null,
    BrokerEnvironmentKind? ManualBrokerEnvironment = null);
