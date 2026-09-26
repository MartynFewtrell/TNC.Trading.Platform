namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed record MarketCategoryViewModel(
    string Code,
    bool NonTradeable,
    bool Interested = false,
    DateTimeOffset? LastSuccessfulCollectionAtUtc = null,
    int Attempts = 0,
    string? CollectionOutcome = null,
    string? SafeFailure = null,
    MarketDetailCoverageViewModel? DetailCoverage = null);
