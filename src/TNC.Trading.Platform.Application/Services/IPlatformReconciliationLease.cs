namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformReconciliationLease
{
    Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken);
}
