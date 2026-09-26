using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentCycleStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null,
    TimeProvider? timeProvider = null) : IMarketCategoryInstrumentCycleStore
{
    private const int MaximumAttempts = 3;
    private static readonly HashSet<string> SafeErrors = new(StringComparer.Ordinal)
    {
        "ProviderUnavailable", "InvalidResponse", "BudgetExhausted", "WindowClosed", "Conflict", "UnexpectedFailure"
    };
    private TimeProvider Clock => timeProvider ?? TimeProvider.System;

    public async Task<MarketCategoryInstrumentSlotProgress?> GetLatestProgressAsync(
        BrokerEnvironmentKind environment,
        CancellationToken cancellationToken)
    {
        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        var latest = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .OrderByDescending(item => item.TradingDay)
            .ThenByDescending(item => item.ScheduledSlot)
            .Select(item => new
            {
                item.TradingDay,
                item.ScheduledSlot,
                item.ScheduleRevision,
                item.Outcome
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (latest is null || latest.Outcome is "Running" or "Pending")
        {
            return null;
        }

        var frequency = await dbContext.InstrumentCollectionSettings.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => new { item.CurrentUpdatesPerDay, item.PendingUpdatesPerDay, item.PendingEffectiveTradingDay })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var effectiveFrequency = frequency is null
            ? 1
            : frequency.PendingUpdatesPerDay is not null
                && frequency.PendingEffectiveTradingDay is not null
                && latest.TradingDay >= frequency.PendingEffectiveTradingDay
                    ? frequency.PendingUpdatesPerDay.Value
                    : frequency.CurrentUpdatesPerDay;
        return new(
            latest.TradingDay,
            latest.ScheduledSlot,
            effectiveFrequency,
            latest.ScheduleRevision.ToString(CultureInfo.InvariantCulture));
    }

    public Task<long?> TryAcquireLeaseAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken) =>
        TryAcquireLeaseAsync(
            lease.BrokerEnvironment,
            lease.TradingDay,
            lease.ScheduledSlot,
            lease.ScheduleRevision,
            lease.Owner,
            nowUtc,
            leaseDuration,
            cancellationToken,
            lease.WindowEndUtc);

    public Task<bool> TryRenewLeaseAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken) =>
        TryRenewLeaseAsync(
            lease.BrokerEnvironment,
            lease.TradingDay,
            lease.ScheduledSlot,
            lease.Owner,
            lease.Fence,
            nowUtc,
            leaseDuration,
            cancellationToken,
            lease.WindowEndUtc);

    public Task<bool> TryBeginCategoryPrerequisiteAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        TryBeginCategoryPrerequisiteAsync(
            lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot, lease.Owner, lease.Fence,
            nowUtc, cancellationToken, lease.WindowEndUtc);

    public Task<bool> TryReserveCategoryAttemptAsync(
        MarketCategoryInstrumentCycleLease lease,
        string categoryCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        TryReserveCategoryAttemptAsync(
            lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot, categoryCode, lease.Owner, lease.Fence,
            nowUtc, cancellationToken, lease.WindowEndUtc);

    public Task<bool> CompleteCategoryPrerequisiteAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeError,
        CancellationToken cancellationToken) =>
        CompleteCategoryPrerequisiteAsync(
            lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot, lease.Owner, lease.Fence,
            nowUtc, succeeded, safeError, cancellationToken, lease.WindowEndUtc);

    public Task<bool> CompleteCategoryAttemptAsync(
        MarketCategoryInstrumentCycleLease lease,
        string categoryCode,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeError,
        CancellationToken cancellationToken) =>
        CompleteCategoryAttemptAsync(
            lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot, categoryCode, lease.Owner, lease.Fence,
            nowUtc, succeeded, safeError, cancellationToken, lease.WindowEndUtc);

    public async Task<bool> HasRequestBudgetAsync(
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken cancellationToken)
    {
        var environmentId = await ResolveEnvironmentIdAsync(lease.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        var allowance = await dbContext.InstrumentCollectionSettings.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.ApprovedNonTradingDailyRequestAllowance)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var used = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == lease.TradingDay)
            .SumAsync(item => (int?)item.UsedRequestBudget, cancellationToken).ConfigureAwait(false) ?? 0;
        var current = await FindCycleAsync(environmentId, lease.TradingDay, lease.ScheduledSlot, cancellationToken).ConfigureAwait(false);
        return allowance is > 0
            && (long)used < allowance.Value
            && OwnsLiveLease(current, lease.Owner, lease.Fence, Clock.GetUtcNow().ToUniversalTime())
            && Clock.GetUtcNow().ToUniversalTime() < lease.WindowEndUtc;
    }

    public Task<bool> CompleteCycleAsync(
        MarketCategoryInstrumentCycleLease lease,
        DateTimeOffset nowUtc,
        string outcome,
        CancellationToken cancellationToken) =>
        CompleteCycleAsync(
            lease.BrokerEnvironment, lease.TradingDay, lease.ScheduledSlot, lease.Owner, lease.Fence,
            nowUtc, outcome, cancellationToken);

    public async Task RecordMissedSlotsAsync(
        MarketCategoryInstrumentCycleLease lease,
        IReadOnlyList<int> missedSlotIndexes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(missedSlotIndexes);
        if (missedSlotIndexes.Any(slot => slot < 0 || slot >= lease.ScheduledSlot || slot > 3))
        {
            throw new ArgumentOutOfRangeException(nameof(missedSlotIndexes));
        }

        var environmentId = await ResolveEnvironmentIdAsync(lease.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        foreach (var missedSlot in missedSlotIndexes.Distinct())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            await AcquireCycleLockAsync(environmentId, lease.TradingDay, missedSlot, cancellationToken).ConfigureAwait(false);
            var state = await FindCycleAsync(environmentId, lease.TradingDay, missedSlot, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                dbContext.InstrumentCollectionCycleStates.Add(new InstrumentCollectionCycleStateEntity
                {
                    BrokerEnvironmentId = environmentId,
                    TradingDay = lease.TradingDay,
                    ScheduledSlot = missedSlot,
                    ScheduleRevision = lease.ScheduleRevision,
                    Outcome = "Skipped",
                    CategoryPrerequisite = "Skipped"
                });
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (state.ScheduleRevision == lease.ScheduleRevision
                && state.Outcome is "Pending" or "Running")
            {
                state.Outcome = "Skipped";
                state.CategoryPrerequisite = "Skipped";
                state.LeaseOwner = null;
                state.LeaseExpiresAtUtc = null;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<long?> TryAcquireLeaseAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        long scheduleRevision,
        Guid leaseOwner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        ValidateLeaseRequest(scheduledSlot, scheduleRevision, leaseOwner, nowUtc, leaseDuration);
        if (windowEndUtc is { } windowEnd && (windowEnd.Offset != TimeSpan.Zero || nowUtc >= windowEnd))
        {
            return null;
        }
        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var state = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            state = new InstrumentCollectionCycleStateEntity
            {
                BrokerEnvironmentId = environmentId,
                TradingDay = tradingDay,
                ScheduledSlot = scheduledSlot,
                ScheduleRevision = scheduleRevision
            };
            dbContext.InstrumentCollectionCycleStates.Add(state);
        }
        else if (state.ScheduleRevision != scheduleRevision
            || state.Outcome is "Completed" or "Skipped" or "Idle"
            || (state.LeaseOwner is not null && state.LeaseExpiresAtUtc > nowUtc))
        {
            return null;
        }

        state.LeaseFence = checked(state.LeaseFence + 1);
        state.LeaseOwner = leaseOwner;
        state.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        state.Outcome = "Running";
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return state.LeaseFence;
    }

    internal async Task<bool> TryRenewLeaseAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        ValidateLeaseRequest(scheduledSlot, 0, leaseOwner, nowUtc, leaseDuration);
        if (windowEndUtc is { } windowEnd && (windowEnd.Offset != TimeSpan.Zero || nowUtc >= windowEnd))
        {
            return false;
        }
        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var state = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(state, leaseOwner, leaseFence, nowUtc))
        {
            return false;
        }

        state!.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> TryBeginCategoryPrerequisiteAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        if (windowEndUtc is { } windowEnd && nowUtc >= windowEnd)
        {
            return false;
        }

        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var state = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(state, leaseOwner, leaseFence, nowUtc)
            || state!.CategoryPrerequisite is "Succeeded"
            || (state.CategoryPrerequisite == "Running" && state.CategoryPrerequisiteLeaseFence == leaseFence)
            || state.CategoryPrerequisiteAttempts >= MaximumAttempts)
        {
            return false;
        }

        state.CategoryPrerequisiteAttempts++;
        state.CategoryPrerequisite = "Running";
        state.CategoryPrerequisiteLeaseFence = leaseFence;
        state.Outcome = "Running";
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> TryReserveCategoryAttemptAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        string categoryCode,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        if (windowEndUtc is { } windowEnd && nowUtc >= windowEnd)
        {
            return false;
        }

        ValidateCategoryCode(categoryCode);
        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var cycle = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(cycle, leaseOwner, leaseFence, nowUtc) || cycle!.CategoryPrerequisite != "Succeeded")
        {
            return false;
        }

        var attempt = await dbContext.InstrumentCollectionCategoryAttempts.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == tradingDay
                && item.ScheduledSlot == scheduledSlot
                && item.CategoryCode == categoryCode,
            cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            attempt = new InstrumentCollectionCategoryAttemptEntity
            {
                BrokerEnvironmentId = environmentId,
                TradingDay = tradingDay,
                ScheduledSlot = scheduledSlot,
                CategoryCode = categoryCode
            };
            dbContext.InstrumentCollectionCategoryAttempts.Add(attempt);
        }

        if (attempt.State == "Succeeded"
            || (attempt.State == "Running" && attempt.LeaseFence == leaseFence)
            || attempt.Attempts >= MaximumAttempts)
        {
            return false;
        }

        attempt.Attempts++;
        attempt.State = "Running";
        attempt.LeaseFence = leaseFence;
        attempt.SafeError = null;
        attempt.UpdatedAtUtc = nowUtc.ToUniversalTime();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> TryConsumeRequestBudgetAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        int requestCount,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        if (requestCount < 1 || (windowEndUtc is { } windowEnd && nowUtc >= windowEnd))
        {
            return false;
        }

        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, GetBudgetLockResource(environmentId, tradingDay), "Exclusive", cancellationToken).ConfigureAwait(false);
        var dailyRequestAllowance = await dbContext.InstrumentCollectionSettings
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.ApprovedNonTradingDailyRequestAllowance)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (dailyRequestAllowance is null or <= 0)
        {
            return false;
        }

        var cycle = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(cycle, leaseOwner, leaseFence, nowUtc))
        {
            return false;
        }

        var usedToday = await dbContext.InstrumentCollectionCycleStates
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay)
            .SumAsync(item => (int?)item.UsedRequestBudget, cancellationToken).ConfigureAwait(false) ?? 0;
        if ((long)usedToday + requestCount > dailyRequestAllowance.Value)
        {
            return false;
        }

        cycle!.UsedRequestBudget = checked(cycle.UsedRequestBudget + requestCount);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> IsDetailRequestLeaseActiveAsync(
        MarketDetailRequestBudgetContext context,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!IsValidDetailBudgetContext(context, nowUtc))
        {
            return false;
        }

        var environmentId = await ResolveEnvironmentIdAsync(context.Environment, cancellationToken).ConfigureAwait(false);
        var run = await dbContext.MarketDetailCollectionRuns.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.RunId == context.RunId
                && item.BrokerEnvironmentId == environmentId
                && item.TradingDay == context.TradingDay
                && item.ScheduledSlot == context.SlotIndex
                && item.Status == "Collecting"
                && item.EndpointProfile == context.AppliedEndpointProfile
                && item.ScheduleRevision == context.ScheduleRevision
                && item.WindowEndUtc == context.WindowEndUtc
                && item.LeaseOwner == context.LeaseOwner
                && item.LeaseFence == context.LeaseFence
                && item.LeaseExpiresAtUtc > nowUtc,
                cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return false;
        }

        var cycle = await FindCycleAsync(
            environmentId, context.TradingDay, context.SlotIndex, cancellationToken).ConfigureAwait(false);
        if (cycle is null
            || cycle.ScheduleRevision != context.ScheduleRevision
            || cycle.CategoryPrerequisite != "Succeeded"
            || !await EfMarketDetailSourceRevisionGuard.IsCurrentAsync(
                dbContext, environmentId, run, cycle, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            context.Environment,
            context.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<int?> GetRemainingDetailRequestBudgetAsync(
        MarketDetailRequestBudgetContext context,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!await IsDetailRequestLeaseActiveAsync(context, nowUtc, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var environmentId = await ResolveEnvironmentIdAsync(context.Environment, cancellationToken).ConfigureAwait(false);
        var allowance = await dbContext.InstrumentCollectionSettings.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.ApprovedNonTradingDailyRequestAllowance)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (allowance is null)
        {
            return null;
        }

        var used = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == context.TradingDay)
            .SumAsync(item => (int?)item.UsedRequestBudget, cancellationToken).ConfigureAwait(false) ?? 0;
        return (int)Math.Max(0L, (long)allowance.Value - used);
    }

    internal async Task<bool> TryConsumeDetailRequestBudgetAsync(
        MarketDetailRequestBudgetContext context,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!IsValidDetailBudgetContext(context, nowUtc))
        {
            return false;
        }

        var environmentId = await ResolveEnvironmentIdAsync(context.Environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            GetBudgetLockResource(environmentId, context.TradingDay),
            "Exclusive",
            cancellationToken).ConfigureAwait(false);

        var allowance = await dbContext.InstrumentCollectionSettings
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.ApprovedNonTradingDailyRequestAllowance)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (allowance is null or <= 0)
        {
            return false;
        }

        var run = await dbContext.MarketDetailCollectionRuns.SingleOrDefaultAsync(item =>
            item.RunId == context.RunId
            && item.BrokerEnvironmentId == environmentId
            && item.TradingDay == context.TradingDay
            && item.ScheduledSlot == context.SlotIndex
            && item.Status == "Collecting"
            && item.EndpointProfile == context.AppliedEndpointProfile
            && item.ScheduleRevision == context.ScheduleRevision
            && item.WindowEndUtc == context.WindowEndUtc
            && item.LeaseOwner == context.LeaseOwner
            && item.LeaseFence == context.LeaseFence
            && item.LeaseExpiresAtUtc > nowUtc,
            cancellationToken).ConfigureAwait(false);
        var cycle = await FindCycleAsync(
            environmentId, context.TradingDay, context.SlotIndex, cancellationToken).ConfigureAwait(false);
        if (run is null
            || cycle is null
            || cycle.ScheduleRevision != context.ScheduleRevision
            || cycle.CategoryPrerequisite != "Succeeded")
        {
            return false;
        }

        if (!await EfMarketDetailSourceRevisionGuard.IsCurrentAsync(
                dbContext, environmentId, run, cycle, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var usedToday = await dbContext.InstrumentCollectionCycleStates
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == context.TradingDay)
            .SumAsync(item => (int?)item.UsedRequestBudget, cancellationToken).ConfigureAwait(false) ?? 0;
        if ((long)usedToday + 1 > allowance.Value)
        {
            return false;
        }

        run.LeaseExpiresAtUtc = Min(nowUtc.AddMinutes(2), run.WindowEndUtc);
        run.UpdatedAtUtc = nowUtc;
        cycle.UsedRequestBudget = checked(cycle.UsedRequestBudget + 1);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            context.Environment,
            context.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        if (Clock.GetUtcNow().ToUniversalTime() >= context.WindowEndUtc)
        {
            throw new InvalidOperationException("The trading window closed before request budget reservation.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryConsumeManualRequestBudgetAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        long scheduleRevision,
        DateTimeOffset nowUtc,
        DateTimeOffset windowEndUtc,
        int requestCount,
        CancellationToken cancellationToken)
    {
        if (scheduledSlot is < 0 or > 3
            || scheduleRevision <= 0
            || requestCount < 1
            || nowUtc.Offset != TimeSpan.Zero
            || windowEndUtc.Offset != TimeSpan.Zero
            || nowUtc >= windowEndUtc)
        {
            return false;
        }

        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, GetBudgetLockResource(environmentId, tradingDay), "Exclusive", cancellationToken).ConfigureAwait(false);
        var dailyRequestAllowance = await dbContext.InstrumentCollectionSettings
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.ApprovedNonTradingDailyRequestAllowance)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (dailyRequestAllowance is null or <= 0)
        {
            return false;
        }

        var usedToday = await dbContext.InstrumentCollectionCycleStates
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay)
            .SumAsync(item => (int?)item.UsedRequestBudget, cancellationToken).ConfigureAwait(false) ?? 0;
        if ((long)usedToday + requestCount > dailyRequestAllowance.Value)
        {
            return false;
        }

        var cycle = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (cycle is null)
        {
            cycle = new InstrumentCollectionCycleStateEntity
            {
                BrokerEnvironmentId = environmentId,
                TradingDay = tradingDay,
                ScheduledSlot = scheduledSlot,
                ScheduleRevision = scheduleRevision
            };
            dbContext.InstrumentCollectionCycleStates.Add(cycle);
        }

        cycle.UsedRequestBudget = checked(cycle.UsedRequestBudget + requestCount);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        if (Clock.GetUtcNow().ToUniversalTime() >= windowEndUtc)
        {
            throw new InvalidOperationException("The Trading window closed before request budget reservation.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> CompleteCategoryPrerequisiteAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeError,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        if (succeeded && windowEndUtc is { } windowEnd && nowUtc >= windowEnd)
        {
            return false;
        }

        ValidateSafeError(safeError);
        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var state = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(state, leaseOwner, leaseFence, nowUtc)
            || state!.CategoryPrerequisite != "Running"
            || state.CategoryPrerequisiteLeaseFence != leaseFence)
        {
            return false;
        }

        state.CategoryPrerequisite = succeeded ? "Succeeded" : "Failed";
        state.CategoryPrerequisiteSafeError = succeeded ? null : safeError;
        if (!succeeded)
        {
            state.Outcome = "Failed";
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> CompleteCategoryAttemptAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        string categoryCode,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeError,
        CancellationToken cancellationToken,
        DateTimeOffset? windowEndUtc = null)
    {
        if (succeeded && windowEndUtc is { } windowEnd && nowUtc >= windowEnd)
        {
            return false;
        }

        ValidateCategoryCode(categoryCode);
        ValidateSafeError(safeError);
        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var cycle = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var attempt = await dbContext.InstrumentCollectionCategoryAttempts.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == tradingDay
                && item.ScheduledSlot == scheduledSlot
                && item.CategoryCode == categoryCode,
            cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(cycle, leaseOwner, leaseFence, nowUtc)
            || attempt is null
            || attempt.State != "Running"
            || attempt.LeaseFence != leaseFence)
        {
            return false;
        }

        attempt.State = succeeded ? "Succeeded" : "Failed";
        attempt.SafeError = safeError;
        attempt.UpdatedAtUtc = nowUtc.ToUniversalTime();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal async Task<bool> CompleteCycleAsync(
        BrokerEnvironmentKind environment,
        DateOnly tradingDay,
        int scheduledSlot,
        Guid leaseOwner,
        long leaseFence,
        DateTimeOffset nowUtc,
        string outcome,
        CancellationToken cancellationToken)
    {
        if (outcome is not ("Completed" or "Skipped" or "Failed" or "Idle"))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        var environmentId = await ResolveEnvironmentIdAsync(environment, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await AcquireCycleLockAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        var state = await FindCycleAsync(environmentId, tradingDay, scheduledSlot, cancellationToken).ConfigureAwait(false);
        if (!OwnsLiveLease(state, leaseOwner, leaseFence, nowUtc))
        {
            return false;
        }

        if (outcome == "Completed"
            && (state!.CategoryPrerequisite != "Succeeded"
                || await dbContext.InstrumentCollectionCategoryAttempts.AnyAsync(
                    item => item.BrokerEnvironmentId == environmentId
                        && item.TradingDay == tradingDay
                        && item.ScheduledSlot == scheduledSlot
                        && item.State == "Running",
                    cancellationToken).ConfigureAwait(false)))
        {
            return false;
        }

        state!.Outcome = outcome;
        state.LeaseOwner = null;
        state.LeaseExpiresAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await VerifyAppliedAsync(environmentId, environment, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private Task AcquireCycleLockAsync(Guid environmentId, DateOnly tradingDay, int scheduledSlot, CancellationToken cancellationToken) =>
        MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, GetCycleLockResource(environmentId, tradingDay, scheduledSlot), "Exclusive", cancellationToken);

    private Task VerifyAppliedAsync(Guid environmentId, BrokerEnvironmentKind environment, CancellationToken cancellationToken) =>
        EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext, contextResolver, environmentId, environment, null, cancellationToken);

    private static string GetCycleLockResource(Guid environmentId, DateOnly tradingDay, int slot) =>
        $"InstrumentCycle/{environmentId:N}/{tradingDay:yyyyMMdd}/{slot}";

    private static string GetBudgetLockResource(Guid environmentId, DateOnly tradingDay) =>
        $"InstrumentBudget/{environmentId:N}/{tradingDay:yyyyMMdd}";

    private Task<InstrumentCollectionCycleStateEntity?> FindCycleAsync(
        Guid environmentId,
        DateOnly tradingDay,
        int scheduledSlot,
        CancellationToken cancellationToken) =>
        dbContext.InstrumentCollectionCycleStates.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay && item.ScheduledSlot == scheduledSlot,
            cancellationToken);

    private Task<Guid> ResolveEnvironmentIdAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) =>
        EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(dbContext, contextResolver, environment, cancellationToken);

    private static bool OwnsLiveLease(
        InstrumentCollectionCycleStateEntity? state,
        Guid owner,
        long fence,
        DateTimeOffset nowUtc) =>
        state is not null
        && state.LeaseOwner == owner
        && state.LeaseFence == fence
        && state.LeaseExpiresAtUtc > nowUtc;

    private static bool IsValidDetailBudgetContext(
        MarketDetailRequestBudgetContext context,
        DateTimeOffset nowUtc) =>
        context.RunId != Guid.Empty
        && context.LeaseOwner != Guid.Empty
        && context.LeaseFence > 0
        && context.ScheduleRevision > 0
        && context.EffectiveUpdatesPerDay is >= 1 and <= 4
        && !string.IsNullOrWhiteSpace(context.AppliedEndpointProfile)
        && context.WindowEndUtc.Offset == TimeSpan.Zero
        && nowUtc.Offset == TimeSpan.Zero
        && nowUtc < context.WindowEndUtc;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

    private static void ValidateLeaseRequest(int slot, long scheduleRevision, Guid owner, DateTimeOffset nowUtc, TimeSpan duration)
    {
        if (slot < 0 || scheduleRevision < 0 || owner == Guid.Empty || nowUtc.Offset != TimeSpan.Zero || duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("A lease requires a non-negative slot and schedule revision, an owner, UTC time and positive duration.");
        }
    }

    private static void ValidateCategoryCode(string categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode) || categoryCode.Length > 128)
        {
            throw new ArgumentException("A valid category code is required.", nameof(categoryCode));
        }
    }

    private static void ValidateSafeError(string? safeError)
    {
        if (safeError is not null && !SafeErrors.Contains(safeError))
        {
            throw new ArgumentException("Only a known redacted collection error code may be persisted.", nameof(safeError));
        }
    }
}
