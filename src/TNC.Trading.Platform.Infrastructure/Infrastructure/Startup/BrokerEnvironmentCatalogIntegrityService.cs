using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Startup;

internal sealed class BrokerEnvironmentCatalogIntegrityService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<BrokerEnvironmentCatalogIntegrityService> logger)
{
    internal static readonly Guid DemoBrokerEnvironmentId = Guid.Parse("D2A0A8C0-9E0F-4A31-9CE8-2D8F2AF2A001");
    private static readonly Guid DefaultsId = Guid.Parse("8A6AF9D2-2B3C-4D08-9A1A-1D4F2D1D9E01");

    public async Task EnsureRequiredCatalogAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var hasActiveDefaults = await dbContext.BrokerEnvironmentDefaults.AnyAsync(
            item => item.IsActive,
            cancellationToken).ConfigureAwait(false);
        var defaults = await GetOrCreateDefaultsAsync(now, cancellationToken).ConfigureAwait(false);
        var demo = await dbContext.BrokerEnvironments.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == DemoBrokerEnvironmentId,
            cancellationToken).ConfigureAwait(false);
        var changed = !hasActiveDefaults;

        if (demo is null)
        {
            var conflictingDemo = await dbContext.BrokerEnvironments.AnyAsync(
                item => item.NormalizedName == "IG DEMO",
                cancellationToken).ConfigureAwait(false);
            if (conflictingDemo)
            {
                throw new InvalidOperationException(
                    "The broker environment catalog contains an IG Demo record with an unexpected identifier. " +
                    "Correct the catalog record before restarting the platform.");
            }

            demo = new BrokerEnvironmentEntity
            {
                BrokerEnvironmentId = DemoBrokerEnvironmentId,
                Name = "IG Demo",
                NormalizedName = "IG DEMO",
                Provider = "Ig",
                Kind = "Demo",
                Lifecycle = "Active",
                Availability = "Available",
                EndpointProfile = "IgDemo",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.BrokerEnvironments.Add(demo);
            changed = true;
        }

        changed |= await EnsureProfilesAsync(defaults, now, cancellationToken).ConfigureAwait(false);
        changed |= await EnsureSelectionAsync(now, cancellationToken).ConfigureAwait(false);

        if (!changed)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        logger.LogWarning(
            "Recovered missing required IG Demo broker catalog data for broker environment {BrokerEnvironmentId}.",
            DemoBrokerEnvironmentId);
    }

    private async Task<BrokerEnvironmentDefaultsEntity> GetOrCreateDefaultsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var defaults = await dbContext.BrokerEnvironmentDefaults.SingleOrDefaultAsync(
            item => item.IsActive,
            cancellationToken).ConfigureAwait(false);
        if (defaults is not null)
        {
            return defaults;
        }

        var configuration = await dbContext.PlatformConfigurations
            .OrderBy(item => item.ConfigurationId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        defaults = new BrokerEnvironmentDefaultsEntity
        {
            BrokerEnvironmentDefaultsId = DefaultsId,
            Version = 1,
            IsActive = true,
            TradingHoursStart = configuration?.TradingHoursStart ?? new TimeOnly(8, 0),
            TradingHoursEnd = configuration?.TradingHoursEnd ?? new TimeOnly(16, 30),
            TradingDaysCsv = string.IsNullOrWhiteSpace(configuration?.TradingDaysCsv)
                ? "Monday,Tuesday,Wednesday,Thursday,Friday"
                : configuration.TradingDaysCsv,
            WeekendBehavior = string.IsNullOrWhiteSpace(configuration?.WeekendBehavior)
                ? "ExcludeWeekends"
                : configuration.WeekendBehavior,
            BankHolidayExclusionsJson = string.IsNullOrWhiteSpace(configuration?.BankHolidayExclusionsJson)
                ? "[]"
                : configuration.BankHolidayExclusionsJson,
            TimeZone = string.IsNullOrWhiteSpace(configuration?.TimeZone) ? "UTC" : configuration.TimeZone,
            RetryInitialDelaySeconds = configuration?.RetryInitialDelaySeconds ?? 1,
            RetryMaxAutomaticRetries = configuration?.RetryMaxAutomaticRetries ?? 5,
            RetryMultiplier = configuration?.RetryMultiplier ?? 2,
            RetryMaxDelaySeconds = configuration?.RetryMaxDelaySeconds ?? 60,
            RetryPeriodicDelayMinutes = configuration?.RetryPeriodicDelayMinutes ?? 5,
            NotificationProvider = string.IsNullOrWhiteSpace(configuration?.NotificationProvider)
                ? "RecordedOnly"
                : configuration.NotificationProvider,
            NotificationEmailTo = configuration?.NotificationEmailTo,
            CreatedAtUtc = now
        };
        dbContext.BrokerEnvironmentDefaults.Add(defaults);
        return defaults;
    }

    private async Task<bool> EnsureProfilesAsync(
        BrokerEnvironmentDefaultsEntity defaults,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = false;
        if (!await dbContext.BrokerEnvironmentScheduleProfiles.AnyAsync(
                item => item.BrokerEnvironmentId == DemoBrokerEnvironmentId,
                cancellationToken).ConfigureAwait(false))
        {
            dbContext.BrokerEnvironmentScheduleProfiles.Add(new BrokerEnvironmentScheduleProfileEntity
            {
                BrokerEnvironmentId = DemoBrokerEnvironmentId,
                DefaultsVersion = defaults.Version,
                TradingHoursStart = defaults.TradingHoursStart,
                TradingHoursEnd = defaults.TradingHoursEnd,
                TradingDaysCsv = defaults.TradingDaysCsv,
                WeekendBehavior = defaults.WeekendBehavior,
                BankHolidayExclusionsJson = defaults.BankHolidayExclusionsJson,
                TimeZone = defaults.TimeZone
            });
            changed = true;
        }

        if (!await dbContext.BrokerEnvironmentRetryProfiles.AnyAsync(
                item => item.BrokerEnvironmentId == DemoBrokerEnvironmentId,
                cancellationToken).ConfigureAwait(false))
        {
            dbContext.BrokerEnvironmentRetryProfiles.Add(new BrokerEnvironmentRetryProfileEntity
            {
                BrokerEnvironmentId = DemoBrokerEnvironmentId,
                DefaultsVersion = defaults.Version,
                InitialDelaySeconds = defaults.RetryInitialDelaySeconds,
                MaxAutomaticRetries = defaults.RetryMaxAutomaticRetries,
                Multiplier = defaults.RetryMultiplier,
                MaxDelaySeconds = defaults.RetryMaxDelaySeconds,
                PeriodicDelayMinutes = defaults.RetryPeriodicDelayMinutes
            });
            changed = true;
        }

        if (!await dbContext.BrokerEnvironmentNotificationProfiles.AnyAsync(
                item => item.BrokerEnvironmentId == DemoBrokerEnvironmentId,
                cancellationToken).ConfigureAwait(false))
        {
            dbContext.BrokerEnvironmentNotificationProfiles.Add(new BrokerEnvironmentNotificationProfileEntity
            {
                BrokerEnvironmentId = DemoBrokerEnvironmentId,
                DefaultsVersion = defaults.Version,
                Provider = defaults.NotificationProvider,
                EmailTo = defaults.NotificationEmailTo,
                Enabled = false
            });
            changed = true;
        }

        return changed;
    }

    private async Task<bool> EnsureSelectionAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var selection = await dbContext.BrokerEnvironmentSelections.SingleOrDefaultAsync(
            cancellationToken).ConfigureAwait(false);
        if (selection is null)
        {
            dbContext.BrokerEnvironmentSelections.Add(new BrokerEnvironmentSelectionEntity
            {
                SelectedBrokerEnvironmentId = DemoBrokerEnvironmentId,
                AppliedBrokerEnvironmentId = DemoBrokerEnvironmentId,
                RestartRequired = false,
                Version = 1,
                UpdatedAtUtc = now
            });
            return true;
        }

        var changed = false;
        if (selection.SelectedBrokerEnvironmentId is null)
        {
            selection.SelectedBrokerEnvironmentId = DemoBrokerEnvironmentId;
            changed = true;
        }

        if (selection.AppliedBrokerEnvironmentId is null)
        {
            selection.AppliedBrokerEnvironmentId = DemoBrokerEnvironmentId;
            changed = true;
        }

        if (changed)
        {
            selection.Version++;
            selection.UpdatedAtUtc = now;
        }

        return changed;
    }
}
