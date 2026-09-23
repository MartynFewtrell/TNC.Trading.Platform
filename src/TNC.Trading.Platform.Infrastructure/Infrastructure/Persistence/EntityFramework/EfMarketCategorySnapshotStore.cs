using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategorySnapshotStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IMarketCategorySnapshotStore
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
            .Where(item => item.BrokerEnvironmentId == brokerEnvironmentId)
            .Select(item => new MarketCategory(item.Code, item.NonTradeable))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(categories, state.LastRefreshedAtUtc);
    }

    public async Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken)
    {
        var brokerEnvironmentId = await ResolveBrokerEnvironmentIdAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existingCategories = await dbContext.MarketCategories
            .Where(item => item.BrokerEnvironmentId == brokerEnvironmentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.MarketCategories.RemoveRange(existingCategories);

        var state = await dbContext.MarketCategoryCatalogStates
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == brokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false);
        if (state is null)
        {
            dbContext.MarketCategoryCatalogStates.Add(new MarketCategoryCatalogStateEntity
            {
                BrokerEnvironmentId = brokerEnvironmentId,
                LastRefreshedAtUtc = snapshot.LastRefreshedAtUtc ?? throw new InvalidOperationException("A saved category snapshot requires a refresh timestamp.")
            });
        }
        else
        {
            state.LastRefreshedAtUtc = snapshot.LastRefreshedAtUtc ?? throw new InvalidOperationException("A saved category snapshot requires a refresh timestamp.");
        }

        dbContext.MarketCategories.AddRange(snapshot.Categories.Select(category => new MarketCategoryEntity
        {
            BrokerEnvironmentId = brokerEnvironmentId,
            Code = category.Code,
            NonTradeable = category.NonTradeable
        }));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private async Task<Guid> ResolveBrokerEnvironmentIdAsync(CancellationToken cancellationToken)
    {
        var applied = contextResolver is null ? null : await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is not null)
        {
            return applied.BrokerEnvironmentId;
        }

        return await dbContext.BrokerEnvironments
            .Where(item => item.Kind == BrokerEnvironmentKind.Demo.ToString() && item.Availability == "Available")
            .Select(item => (Guid?)item.BrokerEnvironmentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The applied broker environment is unavailable.");
    }
}
