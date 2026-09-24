using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A provider collection that an adapter has validated as complete.</summary>
internal sealed record MarketCategoryInstrumentCollection(
    BrokerEnvironmentKind BrokerEnvironment,
    string CategoryCode,
    MarketCategoryInstrumentCollectionMetadata Metadata,
    IReadOnlyList<MarketCategoryInstrument> Instruments);
