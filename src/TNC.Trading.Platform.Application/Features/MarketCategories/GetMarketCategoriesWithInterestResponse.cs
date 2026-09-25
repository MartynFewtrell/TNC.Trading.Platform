using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed record GetMarketCategoriesWithInterestResponse(
    bool IsAvailable,
    BrokerEnvironmentKind? BrokerEnvironment,
    MarketCategorySnapshot? Snapshot,
    long InterestRevision,
    IReadOnlyList<MarketCategoryInstrumentInterest> Interests,
    MarketCategoryInstrumentCollectionStatus? CollectionStatus);
