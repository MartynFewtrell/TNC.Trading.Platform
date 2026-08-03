using TNC.Trading.Platform.Application.Features.GetPlatformStatus.Ports;

namespace TNC.Trading.Platform.Application.Features.GetPlatformStatus;

internal sealed class GetPlatformStatusHandler(IPlatformStatusProjectionReader projectionReader)
{
    public async Task<GetPlatformStatusResponse> HandleAsync(GetPlatformStatusRequest request, CancellationToken cancellationToken)
    {
        var projection = await projectionReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new GetPlatformStatusResponse(projection.Status, projection.LastReconciledAtUtc);
    }
}
