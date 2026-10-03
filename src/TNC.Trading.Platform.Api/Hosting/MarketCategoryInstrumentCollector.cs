using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
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
        var isStartupCheck = true;
        while (!stoppingToken.IsCancellationRequested)
        {
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
                        result = await coordinator.ExecuteDueCycleAsync(stoppingToken, isStartupCheck).ConfigureAwait(false);
                        isStartupCheck = false;
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

                if (result.FullRunLease is { } fullRunLease)
                {
                    await using var detailScope = serviceScopeFactory.CreateAsyncScope();
                    try
                    {
                        var detailCoordinator = detailScope.ServiceProvider.GetRequiredService<IMarketDetailCollectionCoordinator>();
                        detailResult = await detailCoordinator.ExecuteDueCollectionAsync(
                            stoppingToken,
                            fullRunLease).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Full-run market-detail stage failed.");
                    }

                    var detailsSucceeded = detailResult.Status == MarketDetailRunStatus.Complete
                        && detailResult.Counts.CompletedCount == detailResult.Counts.ExpectedCount;
                    var fullRunStore = detailScope.ServiceProvider.GetRequiredService<IMarketDataFullRunStore>();
                    if (detailResult.Status != MarketDetailRunStatus.Running)
                    {
                        var stageRecorded = await fullRunStore.RecordStageAttemptAsync(
                            fullRunLease,
                            MarketDataFullRunStage.Details,
                            detailsSucceeded ? "Succeeded" : "Failed",
                            timeProvider.GetUtcNow().ToUniversalTime(),
                            detailsSucceeded,
                            detailsSucceeded ? null : "DetailStageIncomplete",
                            stoppingToken).ConfigureAwait(false);
                        if (!stageRecorded)
                        {
                            logger.LogWarning(
                                "Full-run {RunId} could not record its detail-stage outcome; the fenced lease may have expired or the stage attempt limit may have been reached.",
                                fullRunLease.RunId);
                        }

                        var fullRunCompleted = await fullRunStore.CompleteAsync(
                            fullRunLease,
                            result.FailedCategories == 0 && detailsSucceeded ? "Succeeded" : "Partial",
                            result.FailedCategories == 0 && detailsSucceeded ? null : "StageIncomplete",
                            timeProvider.GetUtcNow().ToUniversalTime(),
                            stoppingToken).ConfigureAwait(false);
                        if (!fullRunCompleted)
                        {
                            logger.LogWarning(
                                "Full-run {RunId} could not be completed because its fenced lease is no longer active.",
                                fullRunLease.RunId);
                        }
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
