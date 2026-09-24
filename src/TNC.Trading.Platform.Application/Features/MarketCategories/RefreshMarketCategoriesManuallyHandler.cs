using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed class RefreshMarketCategoriesManuallyHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    PlatformConfigurationService configurationService,
    IMarketCategoryInstrumentFrequencyReader frequencyReader,
    IMarketCategoryInstrumentScheduleGuard scheduleGuard,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy,
    IMarketCategoryInstrumentClock clock,
    RefreshMarketCategoriesHandler refreshHandler)
{
    public async Task<RefreshMarketCategoriesResponse> HandleAsync(
        RefreshMarketCategoriesManuallyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var environment))
        {
            return Failure(MarketCategoriesFailureCategory.UnsupportedEnvironment);
        }

        var configuration = await configurationService.GetRuntimeAsync(null, environment, cancellationToken).ConfigureAwait(false);
        MarketCategoryInstrumentFrequency frequency;
        try
        {
            frequency = await frequencyReader.ReadAsync(environment, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Failure(MarketCategoriesFailureCategory.AllowanceExceeded);
        }

        var decision = schedulePolicy.Evaluate(new(
            true,
            true,
            environment,
            configuration.TradingSchedule,
            frequency,
            null));
        if (!decision.IsDue
            || decision.TradingDay is not { } tradingDay
            || decision.SlotIndex is not { } slot
            || decision.EffectiveUpdatesPerDay is not { } updatesPerDay)
        {
            return Failure(MarketCategoriesFailureCategory.ScheduleClosed);
        }

        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var windowEnd = schedulePolicy.GetWindowEndUtc(configuration.TradingSchedule, tradingDay);
        if (windowEnd is null || windowEnd <= nowUtc)
        {
            return Failure(MarketCategoriesFailureCategory.ScheduleClosed);
        }

        using var deadline = clock.CreateDeadlineCancellationSource(windowEnd.Value - nowUtc);
        using var scheduleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var context = new MarketCategoryInstrumentRequestBudgetContext(
            tradingDay,
            slot,
            Guid.NewGuid(),
            0,
            scheduleCancellation.Token,
            windowEnd,
            MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(configuration.TradingSchedule),
            updatesPerDay,
            applied.EndpointProfile,
            true);
        if (!await scheduleGuard.IsStillActiveAsync(environment, context, scheduleCancellation.Token).ConfigureAwait(false))
        {
            return Failure(MarketCategoriesFailureCategory.ScheduleClosed);
        }

        try
        {
            return await refreshHandler.HandleAsync(
                new(ManualBudgetContext: context, ManualBrokerEnvironment: environment),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (scheduleCancellation.IsCancellationRequested)
        {
            return Failure(MarketCategoriesFailureCategory.ScheduleClosed);
        }
    }

    private static RefreshMarketCategoriesResponse Failure(MarketCategoriesFailureCategory category) =>
        new(new MarketCategoriesRefreshOutcome.Failed(category, "The market category refresh is unavailable."));
}
