using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

internal sealed class NoopPlatformReconciliationLease : IPlatformReconciliationLease
{
    public Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IAsyncDisposable>(new NoopLeaseHandle());
    }

    private sealed class NoopLeaseHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
