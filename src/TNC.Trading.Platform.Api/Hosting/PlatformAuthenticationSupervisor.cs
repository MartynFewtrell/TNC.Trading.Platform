using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

namespace TNC.Trading.Platform.Api.Hosting;

internal sealed class PlatformAuthenticationSupervisor : BackgroundService
{
    private readonly IPlatformAuthenticationSupervisorTickRunner tickRunner;
    private readonly IPlatformAuthenticationSupervisorDelay delay;
    private readonly ILogger<PlatformAuthenticationSupervisor> logger;

    public PlatformAuthenticationSupervisor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PlatformAuthenticationSupervisor> logger,
        IPlatformAuthenticationSupervisorDelay delay)
        : this(new PlatformAuthenticationSupervisorTickRunner(serviceScopeFactory), logger, delay)
    {
    }

    internal PlatformAuthenticationSupervisor(
        IPlatformAuthenticationSupervisorTickRunner tickRunner,
        ILogger<PlatformAuthenticationSupervisor> logger,
        IPlatformAuthenticationSupervisorDelay delay)
    {
        this.tickRunner = tickRunner;
        this.logger = logger;
        this.delay = delay;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await tickRunner.RunSingleTickAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        RunUntilStoppedAsync(stoppingToken);

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
            catch (Exception exception)
            {
                logger.LogError(exception, "Platform auth supervision tick failed.");
            }
        }
    }
}

internal interface IPlatformAuthenticationSupervisorTickRunner
{
    Task RunSingleTickAsync(CancellationToken cancellationToken);
}

internal sealed class PlatformAuthenticationSupervisorTickRunner(
    IServiceScopeFactory serviceScopeFactory) : IPlatformAuthenticationSupervisorTickRunner
{
    public async Task RunSingleTickAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReconcilePlatformAuthenticationHandler>();
        await handler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), cancellationToken).ConfigureAwait(false);
    }
}

internal interface IPlatformAuthenticationSupervisorDelay
{
    Task DelayAsync(CancellationToken cancellationToken);
}

internal sealed class PlatformAuthenticationSupervisorDelay : IPlatformAuthenticationSupervisorDelay
{
    public Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
}