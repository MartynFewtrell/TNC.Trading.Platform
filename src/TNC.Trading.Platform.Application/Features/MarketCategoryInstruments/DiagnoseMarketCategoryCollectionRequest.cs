namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed record DiagnoseMarketCategoryCollectionRequest(
    string CategoryCode,
    int MaximumHttpRequests);
