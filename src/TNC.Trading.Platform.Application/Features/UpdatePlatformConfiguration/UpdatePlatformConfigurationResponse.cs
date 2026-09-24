using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal sealed record UpdatePlatformConfigurationResponse(
    UpdatePlatformConfigurationResult Result,
    BrokerEnvironmentKind? AppliedBrokerEnvironment = null,
    MarketCategoryInstrumentFrequency? InstrumentCollectionFrequency = null,
    string? InstrumentCollectionSettingsStatus = null,
    MarketCategoryInstrumentCollectionStatus? InstrumentCollectionStatus = null);
