using Microsoft.EntityFrameworkCore;
using System.Data;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategorySnapshotStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null,
    TimeProvider? timeProvider = null,
    IMarketCategoryInstrumentScheduleGuard? scheduleGuard = null) : IMarketCategorySnapshotStore
{
    public async Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken)
    {
        var brokerEnvironmentId = await ResolveBrokerEnvironmentIdAsync(cancellationToken).ConfigureAwait(false);
        var state = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == brokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false);
        if (state is null)
        {
            return null;
        }

        var categories = await dbContext.MarketCategories.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == brokerEnvironmentId && item.IsCurrent)
            .Select(item => new MarketCategory(item.Code, item.NonTradeable))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(categories, state.LastRefreshedAtUtc, state.Revision);
    }

    public Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken) =>
        ReplaceCoreAsync(snapshot, null, null, null, cancellationToken);

    public Task<MarketCategorySnapshot> ReplaceScheduledAsync(
        MarketCategorySnapshot snapshot,
        MarketCategoryInstrumentCycleLease lease,
        CancellationToken cancellationToken) =>
        ReplaceCoreAsync(snapshot, lease, null, null, cancellationToken);

    public Task<MarketCategorySnapshot> ReplaceManualScheduledAsync(
        MarketCategorySnapshot snapshot,
        BrokerEnvironmentKind appliedBrokerEnvironment,
        MarketCategoryInstrumentRequestBudgetContext requestBudgetContext,
        CancellationToken cancellationToken) =>
        ReplaceCoreAsync(snapshot, null, appliedBrokerEnvironment, requestBudgetContext, cancellationToken);

    private async Task<MarketCategorySnapshot> ReplaceCoreAsync(
        MarketCategorySnapshot snapshot,
        MarketCategoryInstrumentCycleLease? lease,
        BrokerEnvironmentKind? manualEnvironment,
        MarketCategoryInstrumentRequestBudgetContext? manualContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var nowUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime();
        var fullRunLease = lease?.FullRunLease ?? manualContext?.FullRunLease;
        var windowEndUtc = lease?.WindowEndUtc ?? manualContext?.ScheduleWindowEndUtc;
        if (fullRunLease is null
            && windowEndUtc is { } windowEnd
            && (nowUtc >= windowEnd || windowEnd.Offset != TimeSpan.Zero))
        {
            throw new InvalidOperationException("The Trading window closed before category snapshot publication.");
        }

        if (fullRunLease is null)
        {
            await EnsureManualScheduleActiveAsync(manualEnvironment, manualContext, cancellationToken).ConfigureAwait(false);
        }

        var brokerEnvironmentId = await ResolveBrokerEnvironmentIdAsync(cancellationToken).ConfigureAwait(false);
        var operationEnvironment = lease?.BrokerEnvironment ?? manualEnvironment;
        var operationDay = lease?.TradingDay ?? manualContext?.TradingDay;
        var operationScheduleRevision = lease?.ScheduleRevision ?? manualContext?.ScheduleRevision;
        var operationProfile = lease?.EndpointProfile ?? manualContext?.AppliedEndpointProfile;
        var operationWindowEnd = lease?.WindowEndUtc ?? manualContext?.ScheduleWindowEndUtc;
        if (fullRunLease is not null
            && (operationEnvironment != fullRunLease.Environment
                || operationDay != fullRunLease.TradingDay
                || operationScheduleRevision != fullRunLease.ScheduleRevision
                || operationProfile != fullRunLease.EndpointProfile
                || operationWindowEnd != fullRunLease.WindowEndUtc
                || brokerEnvironmentId != fullRunLease.AppliedBrokerEnvironmentId))
        {
            throw new InvalidOperationException("The category publication context does not match its admitted full run.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        if (fullRunLease is not null)
        {
            if (!await EfMarketDataFullRunLeaseGuard.IsActiveInTransactionAsync(
                    dbContext,
                    fullRunLease,
                    nowUtc,
                    contextResolver,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The full-run lease is stale before category publication.");
            }
        }
        else
        {
            await MarketCategoryInstrumentSqlLock.AcquireAsync(
                dbContext,
                $"MarketDataFullRuns/{brokerEnvironmentId:N}",
                "Exclusive",
                cancellationToken).ConfigureAwait(false);
            if (await dbContext.MarketDataFullRuns.AsNoTracking().AnyAsync(
                    item => item.BrokerEnvironmentId == brokerEnvironmentId
                        && item.Status == "Running"
                        && item.LeaseExpiresAtUtc > nowUtc,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "A category-only refresh cannot publish while a full market-data run is active.");
            }
        }

        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstruments/{brokerEnvironmentId:N}", "Exclusive", cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstrumentInterests/{brokerEnvironmentId:N}", "Exclusive", cancellationToken).ConfigureAwait(false);
        if (lease is not null)
        {
            await MarketCategoryInstrumentSqlLock.AcquireAsync(
                dbContext,
                $"InstrumentCycle/{brokerEnvironmentId:N}/{lease.TradingDay:yyyyMMdd}/{lease.ScheduledSlot}",
                "Exclusive",
                cancellationToken).ConfigureAwait(false);
            var cycle = await dbContext.InstrumentCollectionCycleStates.SingleOrDefaultAsync(
                item => item.BrokerEnvironmentId == brokerEnvironmentId
                    && item.TradingDay == lease.TradingDay
                    && item.ScheduledSlot == lease.ScheduledSlot,
                cancellationToken).ConfigureAwait(false);
            if (cycle is null
                || cycle.LeaseOwner != lease.Owner
                || cycle.LeaseFence != lease.Fence
                || cycle.LeaseExpiresAtUtc <= nowUtc
                || cycle.Outcome != "Running"
                || cycle.CategoryPrerequisite != "Running"
                || cycle.CategoryPrerequisiteLeaseFence != lease.Fence)
            {
                throw new InvalidOperationException("The category prerequisite lease is stale.");
            }
        }

        if (snapshot.Categories.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count() != snapshot.Categories.Count)
        {
            throw new InvalidOperationException("A market category snapshot cannot contain duplicate category codes.");
        }

        var requestedCategories = snapshot.Categories.ToDictionary(item => item.Code, StringComparer.Ordinal);
        var existingCategories = await dbContext.MarketCategories
            .Where(item => item.BrokerEnvironmentId == brokerEnvironmentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingCodes = existingCategories.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var category in existingCategories)
        {
            category.IsCurrent = requestedCategories.TryGetValue(category.Code, out var requestedCategory);
            if (requestedCategory is not null)
            {
                category.NonTradeable = requestedCategory.NonTradeable;
            }
        }

        dbContext.MarketCategories.AddRange(snapshot.Categories
            .Where(item => !existingCodes.Contains(item.Code))
            .Select(category => new MarketCategoryEntity
            {
                BrokerEnvironmentId = brokerEnvironmentId,
                Code = category.Code,
                NonTradeable = category.NonTradeable,
                IsCurrent = true
            }));

        var state = await dbContext.MarketCategoryCatalogStates
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == brokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false);
        var nextRevision = checked((state?.Revision ?? 0) + 1);
        if (state is null)
        {
            dbContext.MarketCategoryCatalogStates.Add(new MarketCategoryCatalogStateEntity
            {
                BrokerEnvironmentId = brokerEnvironmentId,
                Revision = nextRevision,
                LastRefreshedAtUtc = snapshot.LastRefreshedAtUtc ?? throw new InvalidOperationException("A saved category snapshot requires a refresh timestamp.")
            });
        }
        else
        {
            state.Revision = nextRevision;
            state.LastRefreshedAtUtc = snapshot.LastRefreshedAtUtc ?? throw new InvalidOperationException("A saved category snapshot requires a refresh timestamp.");
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (fullRunLease is null
            && windowEndUtc is { } commitWindowEnd
            && (cancellationToken.IsCancellationRequested
                || (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime() >= commitWindowEnd))
        {
            throw new InvalidOperationException("The Trading window closed before category snapshot commit.");
        }

        await EnsureManualScheduleActiveAsync(manualEnvironment, manualContext, cancellationToken).ConfigureAwait(false);
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            brokerEnvironmentId,
            lease?.BrokerEnvironment
                ?? manualEnvironment
                ?? await GetAppliedEnvironmentKindAsync(cancellationToken).ConfigureAwait(false),
            lease?.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        if (fullRunLease is null
            && windowEndUtc is not null
            && (cancellationToken.IsCancellationRequested
                || (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime() >= windowEndUtc))
        {
            throw new InvalidOperationException("The Trading window closed before category snapshot commit.");
        }

        if (fullRunLease is not null
            && !await EfMarketDataFullRunLeaseGuard.IsActiveInTransactionAsync(
                dbContext,
                fullRunLease,
                (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime(),
                contextResolver,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The full-run lease expired before category publication committed.");
        }

        if (fullRunLease is null)
        {
            await EnsureManualScheduleActiveAsync(manualEnvironment, manualContext, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return snapshot with { Revision = nextRevision };
    }

    private async Task EnsureManualScheduleActiveAsync(
        BrokerEnvironmentKind? environment,
        MarketCategoryInstrumentRequestBudgetContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        if (environment is null
            || scheduleGuard is null
            || !await scheduleGuard.IsStillActiveAsync(environment.Value, context, cancellationToken).ConfigureAwait(false))
        {
            throw new MarketCategoryInstrumentScheduleClosedException();
        }
    }

    private async Task<BrokerEnvironmentKind> GetAppliedEnvironmentKindAsync(CancellationToken cancellationToken)
    {
        if (contextResolver is not null)
        {
            var context = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
            if (context is not null && Enum.TryParse<BrokerEnvironmentKind>(context.Kind, true, out var kind))
            {
                return kind;
            }
        }

        var appliedId = await dbContext.BrokerEnvironmentSelections
            .Where(item => item.SelectionId == 1)
            .Select(item => item.AppliedBrokerEnvironmentId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var kindName = appliedId is null
            ? null
            : await dbContext.BrokerEnvironments.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == appliedId.Value)
                .Select(item => item.Kind)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return Enum.TryParse<BrokerEnvironmentKind>(kindName, true, out var parsed)
            ? parsed
            : throw new InvalidOperationException("The applied broker environment is unavailable.");
    }

    private async Task<Guid> ResolveBrokerEnvironmentIdAsync(CancellationToken cancellationToken)
    {
        var applied = contextResolver is null ? null : await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is not null)
        {
            if (!string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase) || !applied.IsExecutable)
            {
                throw new InvalidOperationException("The applied IG broker environment is unavailable.");
            }

            return applied.BrokerEnvironmentId;
        }

        var appliedId = await dbContext.BrokerEnvironmentSelections
            .Where(item => item.SelectionId == 1)
            .Select(item => item.AppliedBrokerEnvironmentId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var fallback = appliedId is null
            ? null
            : await dbContext.BrokerEnvironments.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == appliedId.Value && item.Provider == "IG" && item.Lifecycle == "Active" && item.Availability == "Available")
                .Select(item => (Guid?)item.BrokerEnvironmentId)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        return fallback ?? throw new InvalidOperationException("The applied broker environment is unavailable.");
    }
}
