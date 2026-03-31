using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.GetPlatformStatus;

internal sealed class GetPlatformStatusHandler(PlatformStateCoordinator coordinator)
{
    public async Task<GetPlatformStatusResponse> HandleAsync(GetPlatformStatusRequest request, CancellationToken cancellationToken)
    {
        var status = await coordinator.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new GetPlatformStatusResponse(status);
    }
}
