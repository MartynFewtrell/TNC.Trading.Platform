using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.Operations.Retention;

internal sealed class OperationalRecordRetentionService : BackgroundService
{
    private readonly Func<CancellationToken, Task> processAsync;
    private readonly Func<CancellationToken, Task> delayAsync;
    private readonly ILogger<OperationalRecordRetentionService> logger;

    public OperationalRecordRetentionService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OperationalRecordRetentionService> logger)
        : this(
            async cancellationToken =>
            {
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OperationalRecordRetentionProcessor>();
                await processor.ApplyAsync(cancellationToken).ConfigureAwait(false);
            },
            logger,
            cancellationToken => Task.Delay(TimeSpan.FromHours(1), cancellationToken))
    {
    }

    internal OperationalRecordRetentionService(
        Func<CancellationToken, Task> processAsync,
        ILogger<OperationalRecordRetentionService> logger,
        Func<CancellationToken, Task> delayAsync)
    {
        this.processAsync = processAsync;
        this.logger = logger;
        this.delayAsync = delayAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await RunUntilStoppedAsync(stoppingToken).ConfigureAwait(false);

    internal async Task RunUntilStoppedAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Operational record retention failed: {ErrorMessage}",
                    OperationalDataRedactor.RedactText(exception.Message) ?? "Unhandled failure.");
            }

            try
            {
                await delayAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}