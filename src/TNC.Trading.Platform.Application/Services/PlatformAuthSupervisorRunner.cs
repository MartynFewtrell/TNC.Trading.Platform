using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformAuthSupervisorTickRunner
{
    Task RunSingleTickAsync(CancellationToken cancellationToken);
}

internal sealed class PlatformAuthSupervisorTickRunner(
    Func<ReconcilePlatformAuthenticationHandler> handlerFactory,
    IPlatformApplicationLogger logger) : IPlatformAuthSupervisorTickRunner
{
    public async Task RunSingleTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            var handler = handlerFactory();
            await handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Platform auth supervision tick failed.");
        }
    }
}

internal interface IPlatformAuthSupervisorDelay
{
    Task DelayAsync(CancellationToken cancellationToken);
}

internal sealed class PlatformAuthSupervisorDelay : IPlatformAuthSupervisorDelay
{
    public Task DelayAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }
}