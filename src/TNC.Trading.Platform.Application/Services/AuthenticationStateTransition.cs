using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed record AuthenticationStateTransition(
    PlatformSessionStatus TargetStatus,
    string? BlockedReason,
    DateTimeOffset TransitionedAtUtc,
    DateTimeOffset? EstablishedAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null);