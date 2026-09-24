namespace TNC.Trading.Platform.Web;

internal sealed record MarketCategoryCollectionStatusViewModel(
    string CategoryCode,
    DateTimeOffset? LastSuccessfulCollectionAtUtc,
    int Attempts,
    string? Outcome,
    string? SafeFailure);
