using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketCategories;

internal sealed class RefreshMarketCategoriesHandler(
    IMarketCategoriesGateway gateway,
    IMarketCategorySnapshotStore snapshotStore,
    TimeProvider timeProvider,
    IMarketCategoryInstrumentScheduleGuard? scheduleGuard = null)
{
    public async Task<RefreshMarketCategoriesResponse> HandleAsync(
        RefreshMarketCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        using var scheduleCancellation = request.ScheduledLease is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.ScheduleCancellationToken);
        var requestContext = request.ScheduledLease is null
            ? request.ManualBudgetContext
            : new MarketCategoryInstrumentRequestBudgetContext(
                request.ScheduledLease.TradingDay,
                request.ScheduledLease.ScheduledSlot,
                request.ScheduledLease.Owner,
                request.ScheduledLease.Fence,
                scheduleCancellation!.Token,
                request.ScheduledLease.WindowEndUtc,
                request.ScheduledLease.ScheduleRevision,
                request.ScheduledLease.EffectiveUpdatesPerDay);
        var token = scheduleCancellation?.Token
            ?? request.ManualBudgetContext?.ScheduleCancellationToken
            ?? cancellationToken;
        if (request.ManualBudgetContext is not null
            && (request.ManualBrokerEnvironment is null
                || scheduleGuard is null
                || !await scheduleGuard.IsStillActiveAsync(
                    request.ManualBrokerEnvironment.Value,
                    request.ManualBudgetContext,
                    token).ConfigureAwait(false)))
        {
            return new(new MarketCategoriesRefreshOutcome.Failed(
                MarketCategoriesFailureCategory.ScheduleClosed,
                "The market category collection window is closed."));
        }
        var result = requestContext is null
            ? await gateway.GetAsync(token).ConfigureAwait(false)
            : await gateway.GetAsync(requestContext, token).ConfigureAwait(false);
        if (result is MarketCategoriesGatewayResult.Failed failed)
        {
            return new(new MarketCategoriesRefreshOutcome.Failed(failed.Category, failed.SafeReason));
        }

        var succeeded = (MarketCategoriesGatewayResult.Succeeded)result;
        if (request.ScheduledLease is { } lease
            && (lease.WindowEndUtc <= timeProvider.GetUtcNow().ToUniversalTime()
                || request.ScheduleCancellationToken.IsCancellationRequested))
        {
            return new(new MarketCategoriesRefreshOutcome.Failed(
                MarketCategoriesFailureCategory.ScheduleClosed,
                "The Trading window closed before category publication."));
        }

        if (request.ManualBudgetContext is { } context
            && (context.ScheduleCancellationToken.IsCancellationRequested
                || context.ScheduleWindowEndUtc is { } windowEnd && timeProvider.GetUtcNow().ToUniversalTime() >= windowEnd
                || request.ManualBrokerEnvironment is null
                || scheduleGuard is null
                || !await scheduleGuard.IsStillActiveAsync(
                    request.ManualBrokerEnvironment.Value,
                    context,
                    token).ConfigureAwait(false)))
        {
            return new(new MarketCategoriesRefreshOutcome.Failed(
                MarketCategoriesFailureCategory.ScheduleClosed,
                "The market category collection window is closed."));
        }

        var snapshot = new MarketCategorySnapshot(succeeded.Categories, timeProvider.GetUtcNow().ToUniversalTime());
        try
        {
            var saved = request.ScheduledLease is { } scheduledLease
                ? await snapshotStore.ReplaceScheduledAsync(snapshot, scheduledLease, token).ConfigureAwait(false)
                : request.ManualBudgetContext is { } manualBudgetContext
                    ? await snapshotStore.ReplaceManualScheduledAsync(
                        snapshot,
                        request.ManualBrokerEnvironment!.Value,
                        manualBudgetContext,
                        token).ConfigureAwait(false)
                    : await snapshotStore.ReplaceAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return new(new MarketCategoriesRefreshOutcome.Saved(saved));
        }
        catch (MarketCategoryInstrumentScheduleClosedException)
        {
            return new(new MarketCategoriesRefreshOutcome.Failed(
                MarketCategoriesFailureCategory.ScheduleClosed,
                "The market category collection window closed before publication."));
        }
    }
}
