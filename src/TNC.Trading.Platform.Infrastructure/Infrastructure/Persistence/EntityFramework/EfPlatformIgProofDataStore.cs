using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfPlatformIgProofDataStore(PlatformDbContext dbContext) : IPlatformIgProofDataStore
{
    public async Task<IgProofDataSnapshot?> GetLatestAsync(
        BrokerEnvironmentKind brokerEnvironment,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.IgProofData
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironment == brokerEnvironment.ToString(),
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
        var environment = brokerEnvironment.ToString();
        var entity = await dbContext.IgProofData
            .SingleOrDefaultAsync(item => item.BrokerEnvironment == environment, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            dbContext.IgProofData.Add(new IgProofDataEntity
            {
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
}
