using System.Data;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal static class EfMarketDataFullRunLeaseGuard
{
    internal static async Task<bool> IsActiveAsync(
        PlatformDbContext dbContext,
        MarketDataFullRunLease lease,
        DateTimeOffset nowUtc,
        IAppliedBrokerEnvironmentContextResolver? contextResolver,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        var isActive = await IsActiveInTransactionAsync(
            dbContext,
            lease,
            nowUtc,
            contextResolver,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return isActive;
    }

    internal static async Task<bool> IsActiveInTransactionAsync(
        PlatformDbContext dbContext,
        MarketDataFullRunLease lease,
        DateTimeOffset nowUtc,
        IAppliedBrokerEnvironmentContextResolver? contextResolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A full-run lease must be validated inside its protected database transaction.");
        }

        if (lease.RunId == Guid.Empty
            || lease.AppliedBrokerEnvironmentId == Guid.Empty
            || lease.LeaseOwner == Guid.Empty
            || lease.LeaseFence < 1
            || nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A valid full-run lease and UTC validation instant are required.", nameof(lease));
        }

        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{lease.AppliedBrokerEnvironmentId:N}",
            "Shared",
            cancellationToken).ConfigureAwait(false);
        var run = await dbContext.MarketDataFullRuns.AsNoTracking().SingleOrDefaultAsync(
            item => item.RunId == lease.RunId
                && item.BrokerEnvironmentId == lease.AppliedBrokerEnvironmentId,
            cancellationToken).ConfigureAwait(false);
        if (run is null
            || run.Status != "Running"
            || run.LeaseOwner != lease.LeaseOwner
            || run.LeaseFence != lease.LeaseFence
            || run.LeaseExpiresAtUtc is not { } expiresAtUtc
            || expiresAtUtc <= nowUtc
            || run.EndpointProfile != lease.EndpointProfile
            || run.TradingDay != lease.TradingDay
            || run.AdmittedAtUtc != lease.AdmittedAtUtc
            || run.WindowEndUtc != lease.WindowEndUtc
            || run.ScheduleRevision != lease.ScheduleRevision
            || run.EffectiveUpdatesPerDay != lease.EffectiveUpdatesPerDay
            || run.CollectionConfigurationVersion != lease.CollectionConfigurationVersion
            || run.InterestRevision != lease.InterestRevision
            || run.Trigger != lease.Trigger.ToString())
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(lease.SelectedCategoryCodes);
        var persistedCategoryCodes = await dbContext.MarketDataFullRunCategories.AsNoTracking()
            .Where(item => item.RunId == lease.RunId)
            .OrderBy(item => item.CategoryCode)
            .Select(item => item.CategoryCode)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var leaseCategoryCodes = lease.SelectedCategoryCodes
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (!persistedCategoryCodes.SequenceEqual(leaseCategoryCodes, StringComparer.Ordinal))
        {
            return false;
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            lease.AppliedBrokerEnvironmentId,
            lease.Environment,
            lease.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}
