using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentStatusReader(
    PlatformDbContext dbContext,
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
        var attempts = await dbContext.InstrumentCollectionCategoryAttempts.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.TradingDay == tradingDay)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var categories = currentSnapshots
            .OrderBy(item => item.CategoryCode, StringComparer.Ordinal)
            .Select(snapshot =>
            {
                var latestAttempt = attempts
                    .Where(item => string.Equals(item.CategoryCode, snapshot.CategoryCode, StringComparison.Ordinal))
                    .OrderByDescending(item => item.ScheduledSlot)
                    .FirstOrDefault();
                return new MarketCategoryInstrumentCategoryStatus(
                    snapshot.CategoryCode,
                    snapshot.LastRefreshedAtUtc,
                    latestAttempt?.Attempts ?? 0,
                    latestAttempt?.State,
                    latestAttempt?.SafeError);
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
