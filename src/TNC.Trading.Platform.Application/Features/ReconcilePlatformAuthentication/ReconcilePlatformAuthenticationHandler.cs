using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

internal sealed class ReconcilePlatformAuthenticationHandler(IPlatformAuthenticationReconciler reconciler)
{
    private static readonly SemaphoreSlim Writer = new(1, 1);

    public async Task<ReconcilePlatformAuthenticationResponse> HandleAsync(
        ReconcilePlatformAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        _ = request;
        await Writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await reconciler.ReconcileAsync(cancellationToken).ConfigureAwait(false);

            return new ReconcilePlatformAuthenticationResponse(
                state.SessionStatus,
                state.IsDegraded,
                state.LatestFailureSummary,
                state.LastValidatedAtUtc);
        }
        finally
        {
            Writer.Release();
        }
    }
}