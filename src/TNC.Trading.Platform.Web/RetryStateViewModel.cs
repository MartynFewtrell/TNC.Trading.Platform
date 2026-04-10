namespace TNC.Trading.Platform.Web;

internal sealed record RetryStateViewModel(
    string Phase,
    int AutomaticAttemptNumber,
    DateTimeOffset? NextRetryAtUtc,
    bool RetryLimitReached,
    bool ManualRetryAvailable);
