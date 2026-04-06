using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class OperationalRecordRetentionService(IServiceScopeFactory serviceScopeFactory, ILogger<OperationalRecordRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OperationalRecordRetentionProcessor>();
                await processor.ApplyAsync(stoppingToken).ConfigureAwait(false);
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

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
