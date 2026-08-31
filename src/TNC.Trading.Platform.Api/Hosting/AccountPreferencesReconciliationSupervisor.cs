using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Hosting;

internal sealed class AccountPreferencesReconciliationSupervisor(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AccountPreferencesReconciliationSupervisor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ReconcileAccountPreferencesHandler>();
                await handler.HandleAsync(new ReconcileAccountPreferencesRequest(), stoppingToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Account preferences reconciliation tick failed."); }
        }
    }
}