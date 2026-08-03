using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetPlatformStatus.Ports;

internal sealed record PlatformStatusProjection(
    PlatformStatusModel? Status,
    DateTimeOffset? LastReconciledAtUtc);