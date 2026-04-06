using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

namespace TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;

internal static class TriggerManualAuthRetryMapping
{
    public static TriggerManualAuthRetryResponse ToResponse(this AppTriggerManualAuthRetry.TriggerManualAuthRetryResponse response)
        => new(response.Result.RetryCycleId);
}
