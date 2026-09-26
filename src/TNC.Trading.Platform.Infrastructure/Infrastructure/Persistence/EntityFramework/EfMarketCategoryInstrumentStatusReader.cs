using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentStatusReader(
    PlatformDbContext dbContext,
    IMarketDetailReader detailReader,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IMarketCategoryInstrumentStatusReader
{
    public async Task<MarketCategoryInstrumentCollectionStatus> ReadAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        DateOnly tradingDay,
        CancellationToken cancellationToken)
    {
        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            appliedBrokerEnvironment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        var cycle = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay)
            .OrderByDescending(item => item.ScheduledSlot)
            .Select(item => new
            {
                item.ScheduledSlot,
                item.Outcome,
                item.CategoryPrerequisite,
                item.CategoryPrerequisiteSafeError,
                item.UsedRequestBudget
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var allowance = await dbContext.InstrumentCollectionSettings.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.ApprovedNonTradingDailyRequestAllowance)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var usedBudget = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay)
            .SumAsync(item => (int?)item.UsedRequestBudget, cancellationToken).ConfigureAwait(false) ?? 0;
        var categoryRefresh = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => (DateTimeOffset?)item.LastRefreshedAtUtc)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var currentSnapshots = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => new { item.CategoryCode, item.LastRefreshedAtUtc })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var categoryCodes = await dbContext.MarketCategories.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var attempts = await dbContext.InstrumentCollectionCategoryAttempts.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var detailCoverage = await detailReader.ReadCategoryCoverageAsync(
            appliedBrokerEnvironment,
            cancellationToken).ConfigureAwait(false);

        var snapshotByCategory = currentSnapshots.ToDictionary(item => item.CategoryCode, StringComparer.Ordinal);
        var coverageByCategory = detailCoverage.ToDictionary(item => item.CategoryCode, StringComparer.Ordinal);
        var categories = categoryCodes
            .Concat(currentSnapshots.Select(item => item.CategoryCode))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .Select(categoryCode =>
            {
                var latestAttempt = attempts
                    .Where(item => string.Equals(item.CategoryCode, categoryCode, StringComparison.Ordinal))
                    .OrderByDescending(item => item.ScheduledSlot)
                    .FirstOrDefault();
                snapshotByCategory.TryGetValue(categoryCode, out var snapshot);
                coverageByCategory.TryGetValue(categoryCode, out var coverage);
                return new MarketCategoryInstrumentCategoryStatus(
                    categoryCode,
                    snapshot?.LastRefreshedAtUtc,
                    latestAttempt?.Attempts ?? 0,
                    latestAttempt?.State,
                    latestAttempt?.SafeError,
                    coverage);
            })
            .ToArray();
        return new(
            appliedBrokerEnvironment,
            tradingDay,
            categoryRefresh,
            cycle?.ScheduledSlot,
            cycle?.Outcome,
            cycle?.CategoryPrerequisite,
            cycle?.CategoryPrerequisiteSafeError,
            usedBudget,
            allowance,
            categories);
    }
}
