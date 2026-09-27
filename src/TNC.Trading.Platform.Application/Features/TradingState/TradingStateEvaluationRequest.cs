using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.TradingState;

internal sealed record TradingStateEvaluationRequest(
    AppliedBrokerEnvironmentContext? AppliedBrokerEnvironment,
    PlatformConfigurationSnapshot Configuration,
    MarketCategoryInstrumentFrequency CollectionFrequency,
    PlatformRuntimeState? RuntimeState,
    DateTimeOffset NowUtc);
