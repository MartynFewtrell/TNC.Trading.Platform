namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal enum MarketDetailUniverseBlockReason
{
    MissingPrerequisite,
    MissingSelectedCategorySource,
    IncompleteListingSource,
    DuplicateCategorySource,
    InvalidEpic,
    TargetLimitExceeded
}
