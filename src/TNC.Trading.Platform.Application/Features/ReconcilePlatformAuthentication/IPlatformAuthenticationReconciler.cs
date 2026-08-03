using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

internal interface IPlatformAuthenticationReconciler
{
    Task<PlatformRuntimeState> ReconcileAsync(CancellationToken cancellationToken);
}