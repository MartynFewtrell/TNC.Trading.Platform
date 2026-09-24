namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal enum UpdateMarketCategoryInterestStatus
{
    Saved,
    CategoryNotFound,
    RevisionConflict,
    AppliedEnvironmentUnavailable
}

internal sealed record UpdateMarketCategoryInterestResponse(
    UpdateMarketCategoryInterestStatus Status,
    long? Revision);
