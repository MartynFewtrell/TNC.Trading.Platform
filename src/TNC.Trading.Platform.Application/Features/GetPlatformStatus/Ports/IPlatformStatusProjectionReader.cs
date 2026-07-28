using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetPlatformStatus.Ports;

internal interface IPlatformStatusProjectionReader
{
    Task<PlatformStatusProjection> ReadAsync(CancellationToken cancellationToken);
}