namespace TNC.Trading.Platform.Web;

internal sealed record MarketDetailAvailabilityViewModel(
    string State,
    DateTimeOffset? DetailRetrievedAtUtc,
    string? SafeFailureCode);
