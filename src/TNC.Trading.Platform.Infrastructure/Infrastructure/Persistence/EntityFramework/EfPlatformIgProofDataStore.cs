using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfPlatformIgProofDataStore(PlatformDbContext dbContext, IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IPlatformIgProofDataStore
{
    public async Task<IgProofDataSnapshot?> GetLatestAsync(
        BrokerEnvironmentKind brokerEnvironment,
        CancellationToken cancellationToken)
    {
        var brokerEnvironmentId = await ResolveBrokerEnvironmentIdAsync(brokerEnvironment, cancellationToken).ConfigureAwait(false);
        var entity = await dbContext.IgProofData
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironmentId == brokerEnvironmentId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new IgProofDataSnapshot(
                entity.PreferredAccountName,
                entity.PreferredAccountId,
                entity.Balance,
                entity.OpenPositionCount,
                entity.RetrievedAtUtc);
    }

    public async Task SaveAsync(
        BrokerEnvironmentKind brokerEnvironment,
        IgProofDataSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var brokerEnvironmentId = await ResolveBrokerEnvironmentIdAsync(brokerEnvironment, cancellationToken).ConfigureAwait(false);
        var environment = brokerEnvironment.ToString();
        var entity = await dbContext.IgProofData
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == brokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            dbContext.IgProofData.Add(new IgProofDataEntity
            {
                BrokerEnvironmentId = brokerEnvironmentId,
                BrokerEnvironment = environment,
                PreferredAccountName = snapshot.PreferredAccountName,
                PreferredAccountId = snapshot.PreferredAccountId,
                Balance = snapshot.Balance,
                OpenPositionCount = snapshot.OpenPositionCount,
                RetrievedAtUtc = snapshot.RetrievedAtUtc
            });
        }
        else
        {
            entity.PreferredAccountName = snapshot.PreferredAccountName;
            entity.PreferredAccountId = snapshot.PreferredAccountId;
            entity.Balance = snapshot.Balance;
            entity.OpenPositionCount = snapshot.OpenPositionCount;
            entity.RetrievedAtUtc = snapshot.RetrievedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid> ResolveBrokerEnvironmentIdAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var applied = contextResolver is null ? null : await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is not null && string.Equals(applied.Kind, brokerEnvironment.ToString(), StringComparison.OrdinalIgnoreCase)) return applied.BrokerEnvironmentId;
        return await dbContext.BrokerEnvironments.Where(item => item.Kind == brokerEnvironment.ToString() && item.Availability == "Available").Select(item => (Guid?)item.BrokerEnvironmentId).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? Guid.Empty;
    }
}
