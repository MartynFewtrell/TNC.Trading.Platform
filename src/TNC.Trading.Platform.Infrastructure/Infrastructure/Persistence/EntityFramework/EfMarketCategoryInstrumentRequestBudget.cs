using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentRequestBudget(
    EfMarketCategoryInstrumentCycleStore cycleStore,
    TimeProvider timeProvider,
    IMarketCategoryInstrumentScheduleGuard? scheduleGuard = null,
    IMarketDataFullRunStore? fullRunStore = null) : IMarketCategoryInstrumentRequestBudget
{
    public async Task<bool> IsExecutionContextStillActiveAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        if (context.ScheduleCancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (context.FullRunLease is { } fullRunLease)
        {
            if (fullRunLease.Environment != environment
                || fullRunLease.TradingDay != context.TradingDay
                || fullRunLease.ScheduleRevision != context.ScheduleRevision
                || fullRunLease.WindowEndUtc != context.ScheduleWindowEndUtc
                || fullRunLease.EndpointProfile != context.AppliedEndpointProfile)
            {
                return false;
            }

            return await cycleStore.IsFullRunLeaseActiveAsync(
                fullRunLease,
                timeProvider.GetUtcNow().ToUniversalTime(),
                cancellationToken).ConfigureAwait(false);
        }

        if (context.ScheduleWindowEndUtc is { } windowEnd
            && timeProvider.GetUtcNow().ToUniversalTime() >= windowEnd)
        {
            return false;
        }

        return context.ScheduleRevision <= 0
            || scheduleGuard is null
            || await scheduleGuard.IsStillActiveAsync(
                environment,
                context,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryReserveAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();
        if (context.ScheduleCancellationToken.IsCancellationRequested
            || (context.FullRunLease is null
                && context.ScheduleWindowEndUtc is { } windowEnd
                && nowUtc >= windowEnd))
        {
            return false;
        }

        if (!await RenewAsync(environment, context, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (context.IsManualOperation)
        {
            return await cycleStore.TryConsumeManualRequestBudgetAsync(
                environment,
                context.TradingDay,
                context.ScheduledSlot,
                context.ScheduleRevision,
                nowUtc,
                context.ScheduleWindowEndUtc ?? DateTimeOffset.MinValue,
                1,
                cancellationToken,
                context.FullRunLease).ConfigureAwait(false);
        }

        return await cycleStore.TryConsumeRequestBudgetAsync(
            environment,
            context.TradingDay,
            context.ScheduledSlot,
            context.LeaseOwner,
            context.LeaseFence,
            nowUtc,
            1,
            cancellationToken,
            context.FullRunLease is null ? context.ScheduleWindowEndUtc : null,
            context.FullRunLease).ConfigureAwait(false);
    }

    public Task<int?> GetRemainingManualRequestBudgetAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken) =>
        cycleStore.GetRemainingManualRequestBudgetAsync(
            environment,
            context,
            timeProvider.GetUtcNow().ToUniversalTime(),
            cancellationToken);

    public async Task<bool> RenewAsync(
        BrokerEnvironmentKind environment,
        MarketCategoryInstrumentRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        if (!await IsExecutionContextStillActiveAsync(environment, context, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (context.IsManualOperation)
        {
            return true;
        }

        var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();
        if (context.FullRunLease is { } fullRunLease
            && (fullRunStore is null
                || !await fullRunStore.TryRenewLeaseAsync(
                    fullRunLease,
                    nowUtc,
                    TimeSpan.FromMinutes(5),
                    cancellationToken).ConfigureAwait(false)))
        {
            return false;
        }

        return await cycleStore.TryRenewLeaseAsync(
            environment,
            context.TradingDay,
            context.ScheduledSlot,
            context.LeaseOwner,
            context.LeaseFence,
            nowUtc,
            TimeSpan.FromMinutes(2),
            cancellationToken,
            context.FullRunLease is null ? context.ScheduleWindowEndUtc : null,
            context.FullRunLease).ConfigureAwait(false);
    }
}
