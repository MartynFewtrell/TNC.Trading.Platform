using System.Data;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketDetailListingSourceReader(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IMarketDetailListingSourceReader
{
    public async Task<MarketDetailListingSourceSnapshot> ReadAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string appliedEndpointProfile,
        CancellationToken cancellationToken) =>
        await ReadAsync(
            key,
            scheduleRevision,
            appliedEndpointProfile,
            frozenCategoryCodes: null,
            cancellationToken).ConfigureAwait(false);

    public async Task<MarketDetailListingSourceSnapshot> ReadAsync(
        MarketDetailRunKey key,
        long scheduleRevision,
        string appliedEndpointProfile,
        IReadOnlyList<string>? frozenCategoryCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(appliedEndpointProfile);

        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            key.Environment,
            cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);

        var catalogue = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);
        var catalogueRevision = catalogue?.Revision ?? -1;
        var interest = await dbContext.MarketCategoryInterestStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);
        var categoryCodes = await dbContext.MarketCategories.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.IsCurrent)
            .Select(item => item.Code)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var currentCategorySet = categoryCodes.ToHashSet(StringComparer.Ordinal);
        var selectedCodes = frozenCategoryCodes is null
            ? await dbContext.MarketCategoryInterests.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == environmentId && currentCategorySet.Contains(item.CategoryCode))
                .OrderBy(item => item.CategoryCode)
                .Select(item => item.CategoryCode)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false)
            : frozenCategoryCodes
                .Where(currentCategorySet.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

        var cycle = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == key.TradingDay
                && item.ScheduledSlot == key.SlotIndex,
                cancellationToken).ConfigureAwait(false);
        var prerequisitesValidated = catalogue is not null
            && interest is not null
            && cycle is not null
            && cycle.ScheduleRevision == scheduleRevision
            && cycle.CategoryPrerequisite == "Succeeded"
            && (cycle.Outcome == "Completed" || (selectedCodes.Length == 0 && cycle.Outcome == "Idle"));

        var sources = new List<MarketDetailListingSource>(selectedCodes.Length);
        foreach (var categoryCode in selectedCodes)
        {
            var currentState = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
                .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId
                    && item.CategoryCode == categoryCode,
                    cancellationToken).ConfigureAwait(false);
            var run = currentState is null
                ? null
                : await dbContext.MarketCategoryInstrumentCollectionRuns.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.CollectionId == currentState.CollectionId
                        && item.BrokerEnvironmentId == environmentId
                        && item.CategoryCode == categoryCode
                        && item.EndpointProfile == appliedEndpointProfile
                        && item.SnapshotVersion == currentState.SnapshotVersion
                        && item.IsComplete
                        && (item.QualityStatus == "CompleteValidated"
                            || item.QualityStatus == "CompleteValidatedWithOptionalValuesMissing"),
                        cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                continue;
            }

            var epics = await dbContext.MarketCategoryInstrumentObservations.AsNoTracking()
                .Where(item => item.CollectionId == run.CollectionId && item.BrokerEnvironmentId == environmentId)
                .OrderBy(item => item.Epic)
                .Select(item => item.Epic)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (epics.Length != run.ResultCount)
            {
                continue;
            }

            var isFresh = run.TradingDay == key.TradingDay
                && run.ScheduledSlot == key.SlotIndex
                && run.CategorySnapshotRevision == catalogueRevision;
            sources.Add(new(categoryCode, run.CollectionId, run.SnapshotVersion, true, epics)
            {
                IsFresh = isFresh
            });
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(
            new(catalogue?.Revision ?? 0, interest?.Revision ?? 0, scheduleRevision),
            prerequisitesValidated,
            selectedCodes.Length > 0,
            sources);
    }
}
