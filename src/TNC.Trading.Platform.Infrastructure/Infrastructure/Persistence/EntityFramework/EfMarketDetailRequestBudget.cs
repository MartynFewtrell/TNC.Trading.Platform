using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketDetailRequestBudget(
    EfMarketCategoryInstrumentCycleStore cycleStore,
    IMarketCategoryInstrumentScheduleGuard? scheduleGuard = null,
    TimeProvider? timeProvider = null,
    IMarketDataFullRunStore? fullRunStore = null) : IMarketDetailRequestBudget
{
    private TimeProvider Clock => timeProvider ?? TimeProvider.System;

    public async Task<bool> IsExecutionContextStillActiveAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        var nowUtc = Clock.GetUtcNow().ToUniversalTime();
        if (context.WindowEndUtc.Offset != TimeSpan.Zero
            || context.ScheduleRevision <= 0
            || context.EffectiveUpdatesPerDay is < 1 or > 4
            || (context.FullRunLease is null && nowUtc >= context.WindowEndUtc))
        {
            return false;
        }

        if (!await cycleStore.IsDetailRequestLeaseActiveAsync(context, nowUtc, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (context.FullRunLease is { } fullRunLease)
        {
            return fullRunLease.Environment == context.Environment
                && fullRunLease.TradingDay == context.TradingDay
                && fullRunLease.ScheduleRevision == context.ScheduleRevision
                && fullRunLease.WindowEndUtc == context.WindowEndUtc
                && string.Equals(
                    fullRunLease.EndpointProfile,
                    context.AppliedEndpointProfile,
                    StringComparison.Ordinal);
        }

        if (scheduleGuard is null)
        {
            return false;
        }

        return await scheduleGuard.IsStillActiveAsync(
            context.Environment,
            new MarketCategoryInstrumentRequestBudgetContext(
                context.TradingDay,
                context.SlotIndex,
                context.LeaseOwner,
                context.LeaseFence,
                cancellationToken,
                context.WindowEndUtc,
                context.ScheduleRevision,
                context.EffectiveUpdatesPerDay,
                context.AppliedEndpointProfile),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<int?> GetRemainingAllowanceAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken) =>
        cycleStore.GetRemainingDetailRequestBudgetAsync(
            context,
            Clock.GetUtcNow().ToUniversalTime(),
            cancellationToken);

    public async Task<bool> TryReserveAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        if (context.FullRunLease is null
            && Clock.GetUtcNow().ToUniversalTime() >= context.WindowEndUtc
            || !await RenewAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await cycleStore.TryConsumeDetailRequestBudgetAsync(
            context,
            Clock.GetUtcNow().ToUniversalTime(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RenewAsync(
        MarketDetailRequestBudgetContext context,
        CancellationToken cancellationToken)
    {
        if (!await IsExecutionContextStillActiveAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var nowUtc = Clock.GetUtcNow().ToUniversalTime();
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

        return await cycleStore.TryRenewDetailRequestLeaseAsync(
            context,
            nowUtc,
            TimeSpan.FromMinutes(2),
            cancellationToken).ConfigureAwait(false);
    }
}
