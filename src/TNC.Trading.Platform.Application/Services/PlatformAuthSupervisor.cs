using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformAuthSupervisor : BackgroundService
{
    private readonly IPlatformAuthSupervisorTickRunner tickRunner;
    private readonly IPlatformAuthSupervisorDelay delay;

    public PlatformAuthSupervisor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PlatformAuthSupervisorTickRunner> logger)
        : this(
            new PlatformAuthSupervisorTickRunner(serviceScopeFactory, logger),
            new PlatformAuthSupervisorDelay())
    {
    }

    internal PlatformAuthSupervisor(
        IPlatformAuthSupervisorTickRunner tickRunner,
        IPlatformAuthSupervisorDelay delay)
    {
        this.tickRunner = tickRunner;
        this.delay = delay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunUntilStoppedAsync(stoppingToken).ConfigureAwait(false);
    }

    internal async Task RunUntilStoppedAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await tickRunner.RunSingleTickAsync(stoppingToken).ConfigureAwait(false);
                await delay.DelayAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
