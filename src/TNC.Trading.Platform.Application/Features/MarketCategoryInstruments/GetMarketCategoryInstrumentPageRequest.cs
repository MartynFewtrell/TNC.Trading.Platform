namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed record GetMarketCategoryInstrumentPageRequest(
    string CategoryCode,
    int PageSize,
    long? CursorSnapshotVersion,
    string? CursorCategoryCode,
    string? CursorAfterEpic,
    string? CursorBrokerEnvironment);
