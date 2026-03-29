using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;

internal sealed class TriggerManualAuthRetryHandler(PlatformStateCoordinator coordinator)
{
    public async Task<TriggerManualAuthRetryResponse> HandleAsync(TriggerManualAuthRetryRequest request, CancellationToken cancellationToken)
    {
        var result = await coordinator.TriggerManualRetryAsync(cancellationToken);
        return new TriggerManualAuthRetryResponse(result.RetryCycleId);
    }
}
