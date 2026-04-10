using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

internal sealed class TriggerManualAuthRetryHandler(PlatformStateCoordinator coordinator)
{
    public async Task<TriggerManualAuthRetryResponse> HandleAsync(TriggerManualAuthRetryRequest request, CancellationToken cancellationToken)
    {
        var result = await coordinator.TriggerManualRetryAsync(cancellationToken).ConfigureAwait(false);
        return new TriggerManualAuthRetryResponse(result);
    }
}
