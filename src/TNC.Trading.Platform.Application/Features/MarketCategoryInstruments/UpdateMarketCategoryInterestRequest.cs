namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed record UpdateMarketCategoryInterestRequest(
    string CategoryCode,
    bool Interested,
    long ExpectedRevision);
