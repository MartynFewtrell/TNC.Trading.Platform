using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;
using AppTriggerManualAuthRetry = TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class TriggerManualAuthRetryEndpointHandler
{
    public static async Task<Results<Accepted<TriggerManualAuthRetryResponse>, Conflict<ManualAuthRetryConflictResponse>>> HandleAsync(
        AppTriggerManualAuthRetry.TriggerManualAuthRetryHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(new AppTriggerManualAuthRetry.TriggerManualAuthRetryRequest(), cancellationToken);
        return response.Outcome.IsAccepted
            ? TypedResults.Accepted("/api/platform/status", response.ToResponse())
            : TypedResults.Conflict(new ManualAuthRetryConflictResponse(response.Outcome.RejectionReason!.Value.ToConflictMessage()));
    }

    internal static async Task<Results<Accepted<TriggerManualAuthRetryResponse>, Conflict<ManualAuthRetryConflictResponse>>> HandleAsync(
        Func<CancellationToken, Task<AppTriggerManualAuthRetry.TriggerManualAuthRetryResponse>> execute,
        CancellationToken cancellationToken)
    {
        var response = await execute(cancellationToken);
        return response.Outcome.IsAccepted
            ? TypedResults.Accepted("/api/platform/status", response.ToResponse())
            : TypedResults.Conflict(new ManualAuthRetryConflictResponse(response.Outcome.RejectionReason!.Value.ToConflictMessage()));
    }
}

internal static class ManualAuthRetryRejectionReasonMapping
{
    public static string ToConflictMessage(this AppTriggerManualAuthRetry.ManualAuthRetryRejectionReason reason) => reason switch
    {
        AppTriggerManualAuthRetry.ManualAuthRetryRejectionReason.ScheduleInactive => "Manual retry is unavailable while the trading schedule is inactive.",
        AppTriggerManualAuthRetry.ManualAuthRetryRejectionReason.BlockedLive => "IG live is unavailable while the platform environment is Test.",
        AppTriggerManualAuthRetry.ManualAuthRetryRejectionReason.RetryLimitNotReached => "Manual retry becomes available only after the initial automatic retries are exhausted.",
        _ => "Manual retry is unavailable."
    };
}