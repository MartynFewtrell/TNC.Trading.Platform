namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record AuthStateResponse(
    string SessionStatus,
    bool IsDegraded,
    string? BlockedReason);
