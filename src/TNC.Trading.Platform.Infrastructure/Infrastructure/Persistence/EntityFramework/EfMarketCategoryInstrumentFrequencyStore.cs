using Microsoft.EntityFrameworkCore;
using System.Data;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentFrequencyStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) :
    IMarketCategoryInstrumentFrequencyReader,
    IMarketCategoryInstrumentFrequencyWriter
{
    public async Task<MarketCategoryInstrumentFrequency> ReadAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        CancellationToken cancellationToken)
    {
        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext, contextResolver, appliedBrokerEnvironment, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        var settings = await dbContext.InstrumentCollectionSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Instrument collection settings have not been initialized for the applied broker environment.");
        ValidateSettings(settings);
        return new(
            settings.CurrentUpdatesPerDay,
            settings.PendingUpdatesPerDay,
            settings.PendingEffectiveTradingDay,
            settings.ApprovedNonTradingDailyRequestAllowance);
    }

    public async Task SaveAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        MarketCategoryInstrumentFrequency frequency,
        CancellationToken cancellationToken)
    {
        ValidateFrequency(frequency);
        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext, contextResolver, appliedBrokerEnvironment, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstrumentSettings/{environmentId:N}", "Exclusive", cancellationToken).ConfigureAwait(false);
        var settings = await dbContext.InstrumentCollectionSettings
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            throw new InvalidOperationException("Instrument collection settings must be initialized before they can be updated.");
        }

        ValidateSettings(settings);
        if (settings.CurrentUpdatesPerDay != frequency.CurrentUpdatesPerDay)
        {
            throw new InvalidOperationException("The current collection frequency changed; refresh settings before saving a pending frequency.");
        }

        settings.PendingUpdatesPerDay = frequency.PendingUpdatesPerDay;
        settings.PendingEffectiveTradingDay = frequency.PendingEffectiveTradingDay;
        settings.ApprovedNonTradingDailyRequestAllowance = frequency.ApprovedNonTradingDailyRequestAllowance;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext, contextResolver, environmentId, appliedBrokerEnvironment, null, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task InitializeDefaultAsync(
        BrokerEnvironmentKind appliedBrokerEnvironment,
        CancellationToken cancellationToken)
    {
        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext, contextResolver, appliedBrokerEnvironment, cancellationToken, requireExecutable: false).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstrumentSettings/{environmentId:N}", "Exclusive", cancellationToken).ConfigureAwait(false);
        if (await dbContext.InstrumentCollectionSettings.AnyAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        dbContext.InstrumentCollectionSettings.Add(new InstrumentCollectionSettingsEntity
        {
            BrokerEnvironmentId = environmentId,
            CurrentUpdatesPerDay = 1
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateFrequency(MarketCategoryInstrumentFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(frequency);
        if (frequency.CurrentUpdatesPerDay is < 1 or > 4
            || frequency.PendingUpdatesPerDay is < 1 or > 4
            || (frequency.PendingUpdatesPerDay is null) != (frequency.PendingEffectiveTradingDay is null)
            || frequency.ApprovedNonTradingDailyRequestAllowance is < 0)
        {
            throw new ArgumentException("Collection frequency must be between 1 and 4, and pending frequency/date and allowance values must be valid.", nameof(frequency));
        }
    }

    private static void ValidateSettings(InstrumentCollectionSettingsEntity settings)
    {
        if (settings.CurrentUpdatesPerDay is < 1 or > 4
            || settings.PendingUpdatesPerDay is < 1 or > 4
            || (settings.PendingUpdatesPerDay is null) != (settings.PendingEffectiveTradingDay is null)
            || settings.ApprovedNonTradingDailyRequestAllowance is < 0)
        {
            throw new InvalidOperationException("Persisted collection settings are corrupt; collection is paused.");
        }
    }
}
