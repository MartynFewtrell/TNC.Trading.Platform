namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Provider paging evidence used to establish that every expected page and result was collected.</summary>
internal sealed record MarketCategoryInstrumentCollectionMetadata(
    int PageSize,
    IReadOnlyList<int> PageNumbersFetched,
    int ProviderTotalPages,
    int ProviderTotalResults);
