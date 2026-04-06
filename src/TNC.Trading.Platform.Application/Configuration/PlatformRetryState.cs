namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformRetryState(
    AuthRetryPhase Phase,
    int AutomaticAttemptNumber,
    DateTimeOffset? NextRetryAtUtc,
    bool RetryLimitReached,
    bool ManualRetryAvailable);
