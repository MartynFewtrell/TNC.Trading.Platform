using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentRequestBudget(
    EfMarketCategoryInstrumentCycleStore cycleStore,
    TimeProvider timeProvider,
    IMarketCategoryInstrumentScheduleGuard? scheduleGuard = null) : IMarketCategoryInstrumentRequestBudget
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

        if (!await IsExecutionContextStillActiveAsync(environment, context, cancellationToken).ConfigureAwait(false))
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

        if (!await cycleStore.TryRenewLeaseAsync(
                environment,
                context.TradingDay,
                context.ScheduledSlot,
                context.LeaseOwner,
                context.LeaseFence,
                nowUtc,
                TimeSpan.FromMinutes(2),
                cancellationToken,
                context.FullRunLease is null ? context.ScheduleWindowEndUtc : null,
                context.FullRunLease).ConfigureAwait(false))
        {
            return false;
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
}
