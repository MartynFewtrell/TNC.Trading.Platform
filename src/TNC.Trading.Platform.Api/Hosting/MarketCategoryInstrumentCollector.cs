using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Api.Hosting;

internal sealed class MarketCategoryInstrumentCollector(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<MarketCategoryInstrumentCollector> logger) : BackgroundService
{
    private static readonly TimeSpan ConfigurationRecheckInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = timeProvider.GetTimestamp();
            var result = new MarketCategoryInstrumentCycleResult(
                "PausedAfterUnexpectedFailure",
                timeProvider.GetUtcNow().Add(ConfigurationRecheckInterval),
                0,
                0);
            var detailResult = new CollectMarketDetailsResponse(
                MarketDetailRunStatus.NeverCollected,
                new(0, 0, 0),
                "NotAttempted",
                timeProvider.GetUtcNow().Add(ConfigurationRecheckInterval));
            try
            {
                await using (var scope = serviceScopeFactory.CreateAsyncScope())
                {
                    try
                    {
                        var coordinator = scope.ServiceProvider.GetRequiredService<IMarketCategoryInstrumentCycleCoordinator>();
                        result = await coordinator.ExecuteDueCycleAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Scheduled market-category instrument collection tick failed.");
                    }
                }

                await using (var detailScope = serviceScopeFactory.CreateAsyncScope())
                {
                    try
                    {
                        var detailCoordinator = detailScope.ServiceProvider.GetRequiredService<IMarketDetailCollectionCoordinator>();
                        detailResult = await detailCoordinator.ExecuteDueCollectionAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Scheduled market-detail collection tick failed.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled market-category instrument collection tick failed.");
            }

            logger.LogInformation(
                "Instrument collector tick {Status}: {CompletedCategoryCount} categories completed, {FailedCategoryCount} failed, {ProviderPageCount} provider pages, collection IDs {CollectionIds}, elapsed {ElapsedMilliseconds} ms; next schedule check at {NextWakeUpUtc}.",
                result.Status,
                result.CompletedCategories,
                result.FailedCategories,
                result.ProviderPages,
                result.CollectionIds is null ? string.Empty : string.Join(",", result.CollectionIds),
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
                result.NextWakeUpUtc);
            logger.LogInformation(
                "Market-detail collector tick {Status}: {CompletedCount}/{ExpectedCount} EPICs completed, {ExcludedCount} excluded; reason {SafeReasonCode}, next check at {NextScheduledCheckUtc}.",
                detailResult.Status,
                detailResult.Counts.CompletedCount,
                detailResult.Counts.ExpectedCount,
                detailResult.Counts.ExcludedCount,
                detailResult.SafeReasonCode ?? string.Empty,
                detailResult.NextScheduledCheckUtc);

            var requestedDelay = result.NextWakeUpUtc - timeProvider.GetUtcNow();
            var delay = requestedDelay <= TimeSpan.Zero
                ? TimeSpan.FromMilliseconds(1)
                : requestedDelay < ConfigurationRecheckInterval
                    ? requestedDelay
                    : ConfigurationRecheckInterval;
            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
