using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;

internal sealed record GetPlatformConfigurationResponse(
    PlatformConfigurationSnapshot Configuration,
    BrokerEnvironmentKind? AppliedBrokerEnvironment = null,
    MarketCategoryInstrumentFrequency? InstrumentCollectionFrequency = null,
    string? InstrumentCollectionSettingsStatus = null,
    MarketCategoryInstrumentCollectionStatus? InstrumentCollectionStatus = null);
