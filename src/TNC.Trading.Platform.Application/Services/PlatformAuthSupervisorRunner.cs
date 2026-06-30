using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformAuthSupervisorTickRunner
{
    Task RunSingleTickAsync(CancellationToken cancellationToken);
}

internal sealed class PlatformAuthSupervisorTickRunner(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PlatformAuthSupervisorTickRunner> logger) : IPlatformAuthSupervisorTickRunner
{
    public async Task RunSingleTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<PlatformStateCoordinator>();
            await coordinator.TickAsync(cancellationToken).ConfigureAwait(false);
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