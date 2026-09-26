namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailListingSourceSnapshot(
    MarketDetailRevisions Revisions,
    bool PrerequisitesValidated,
    bool HasSelectedCurrentCategories,
    IReadOnlyList<MarketDetailListingSource> Sources);
