namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record RetryStateResponse(
    string Phase,
    int AutomaticAttemptNumber,
    DateTimeOffset? NextRetryAtUtc,
    bool RetryLimitReached,
    bool ManualRetryAvailable);
