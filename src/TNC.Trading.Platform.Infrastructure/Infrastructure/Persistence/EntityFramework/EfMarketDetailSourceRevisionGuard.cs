using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal static class EfMarketDetailSourceRevisionGuard
{
    internal static async Task<bool> IsCurrentAsync(
        PlatformDbContext dbContext,
        Guid environmentId,
        MarketDetailCollectionRunEntity detailRun,
        InstrumentCollectionCycleStateEntity cycle,
        CancellationToken cancellationToken)
    {
        var catalogueRevision = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => (long?)item.Revision)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var interestRevision = await dbContext.MarketCategoryInterestStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => (long?)item.Revision)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (catalogueRevision != detailRun.CatalogueRevision
            || interestRevision != detailRun.InterestRevision)
        {
            return false;
        }

        var selectedCategories = await dbContext.MarketCategoryInterests.AsNoTracking()
            .Where(interest => interest.BrokerEnvironmentId == environmentId
                && dbContext.MarketCategories.Any(category =>
                    category.BrokerEnvironmentId == environmentId
                    && category.Code == interest.CategoryCode))
            .OrderBy(item => item.CategoryCode)
            .Select(item => item.CategoryCode)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var sources = await dbContext.MarketDetailRunSources.AsNoTracking()
            .Where(item => item.RunId == detailRun.RunId && item.BrokerEnvironmentId == environmentId)
            .OrderBy(item => item.CategoryCode)
            .Select(item => new
            {
                item.CategoryCode,
                item.ListingCollectionId,
                item.ListingVersion,
                item.IsValidatedComplete
            })
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!sources.All(source => source.IsValidatedComplete)
            || !selectedCategories.SequenceEqual(
                sources.Select(source => source.CategoryCode),
                StringComparer.Ordinal)
            || (cycle.Outcome != "Completed"
                && !(cycle.Outcome == "Idle" && selectedCategories.Length == 0)))
        {
            return false;
        }

        foreach (var source in sources)
        {
            var current = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == environmentId
                    && item.CategoryCode == source.CategoryCode)
                .Select(item => new { item.CollectionId, item.SnapshotVersion })
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (current is null
                || current.CollectionId != source.ListingCollectionId
                || current.SnapshotVersion != source.ListingVersion)
            {
                return false;
            }

            var listingRun = await dbContext.MarketCategoryInstrumentCollectionRuns.AsNoTracking()
                .Where(item => item.CollectionId == source.ListingCollectionId
                    && item.BrokerEnvironmentId == environmentId
                    && item.CategoryCode == source.CategoryCode
                    && item.TradingDay == detailRun.TradingDay
                    && item.ScheduledSlot == detailRun.ScheduledSlot
                    && item.CategorySnapshotRevision == detailRun.CatalogueRevision
                    && item.EndpointProfile == detailRun.EndpointProfile
                    && item.SnapshotVersion == source.ListingVersion
                    && item.IsComplete
                    && (item.QualityStatus == "CompleteValidated"
                        || item.QualityStatus == "CompleteValidatedWithOptionalValuesMissing"))
                .Select(item => new { item.CollectionId, item.ResultCount })
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (listingRun is null)
            {
                return false;
            }

            var observationCount = await dbContext.MarketCategoryInstrumentObservations.AsNoTracking()
                .CountAsync(item => item.CollectionId == listingRun.CollectionId
                    && item.BrokerEnvironmentId == environmentId,
                    cancellationToken).ConfigureAwait(false);
            if (observationCount != listingRun.ResultCount)
            {
                return false;
            }
        }

        return true;
    }
}
