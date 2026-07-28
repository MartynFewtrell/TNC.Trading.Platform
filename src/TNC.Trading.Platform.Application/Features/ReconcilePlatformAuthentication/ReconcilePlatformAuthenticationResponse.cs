using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

internal sealed record ReconcilePlatformAuthenticationResponse(
    PlatformSessionStatus SessionStatus,
    bool IsDegraded,
    string? FailureSummary,
    DateTimeOffset? ReconciledAtUtc);