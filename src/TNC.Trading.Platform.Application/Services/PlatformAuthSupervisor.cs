using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformAuthSupervisor(IServiceScopeFactory serviceScopeFactory, ILogger<PlatformAuthSupervisor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<PlatformStateCoordinator>();
                await coordinator.TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Platform auth supervision tick failed: {ErrorMessage}",
                    exception.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
