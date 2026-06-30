using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;
using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class TriggerManualAuthRetryEndpointHandler
{
    public static async Task<Results<Accepted<TriggerManualAuthRetryResponse>, Conflict<ManualAuthRetryConflictResponse>>> HandleAsync(
        AppTriggerManualAuthRetry.TriggerManualAuthRetryHandler handler,
        CancellationToken cancellationToken)
        => await HandleAsync(
            async currentCancellationToken =>
            {
                var response = await handler.HandleAsync(new AppTriggerManualAuthRetry.TriggerManualAuthRetryRequest(), currentCancellationToken);
                return response.ToResponse();
            },
            cancellationToken);

    internal static async Task<Results<Accepted<TriggerManualAuthRetryResponse>, Conflict<ManualAuthRetryConflictResponse>>> HandleAsync(
        Func<CancellationToken, Task<TriggerManualAuthRetryResponse>> execute,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await execute(cancellationToken);
            return TypedResults.Accepted("/api/platform/status", response);
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.Conflict(new ManualAuthRetryConflictResponse(exception.Message));
        }
    }
}