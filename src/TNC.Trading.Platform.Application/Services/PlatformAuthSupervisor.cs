namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformAuthSupervisor
{
    private readonly IPlatformAuthSupervisorTickRunner tickRunner;
    private readonly IPlatformAuthSupervisorDelay delay;

    internal PlatformAuthSupervisor(
        IPlatformAuthSupervisorTickRunner tickRunner,
        IPlatformAuthSupervisorDelay delay)
    {
        this.tickRunner = tickRunner;
        this.delay = delay;
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
