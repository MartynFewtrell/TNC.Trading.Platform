using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal sealed class SqlPlatformConfigurationStore(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    IProtectedCredentialService protectedCredentialService,
    TimeProvider timeProvider) : IPlatformConfigurationStore, IUpdatePlatformConfigurationCommitter
{
    public async Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken).ConfigureAwait(false);
        if (entity.RestartRequired)
        {
            entity.RestartRequired = false;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return await MapAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken).ConfigureAwait(false);
        return await MapAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
        PlatformEnvironmentKind? platformEnvironment,
        BrokerEnvironmentKind? brokerEnvironment,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken).ConfigureAwait(false);

        if (!entity.RestartRequired || platformEnvironment is null || brokerEnvironment is null)
        {
            return await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        return await MapAsync(entity, platformEnvironment.Value, brokerEnvironment.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UpdatePlatformConfigurationResult> CommitAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var currentStartupFixedConfiguration = new PlatformStartupFixedConfiguration(
            Enum.Parse<PlatformEnvironmentKind>(entity.PlatformEnvironment, ignoreCase: true),
            Enum.Parse<BrokerEnvironmentKind>(entity.BrokerEnvironment, ignoreCase: true));
        var restartRequired = PlatformConfigurationRestartPolicy.IsRestartRequired(currentStartupFixedConfiguration, update);

        entity.PlatformEnvironment = update.PlatformEnvironment.ToString();
        entity.BrokerEnvironment = update.BrokerEnvironment.ToString();
        entity.TradingHoursStart = update.TradingSchedule.StartOfDay;
        entity.TradingHoursEnd = update.TradingSchedule.EndOfDay;
        entity.TradingDaysCsv = string.Join(',', update.TradingSchedule.TradingDays);
        entity.WeekendBehavior = update.TradingSchedule.WeekendBehavior.ToString();
        entity.BankHolidayExclusionsJson = JsonSerializer.Serialize(update.TradingSchedule.BankHolidayExclusions);
        entity.TimeZone = update.TradingSchedule.TimeZone;
        entity.RetryInitialDelaySeconds = update.RetryPolicy.InitialDelaySeconds;
        entity.RetryMaxAutomaticRetries = update.RetryPolicy.MaxAutomaticRetries;
        entity.RetryMultiplier = update.RetryPolicy.Multiplier;
        entity.RetryMaxDelaySeconds = update.RetryPolicy.MaxDelaySeconds;
        entity.RetryPeriodicDelayMinutes = update.RetryPolicy.PeriodicDelayMinutes;
        entity.NotificationProvider = update.NotificationSettings.Provider;
        entity.NotificationEmailTo = update.NotificationSettings.EmailTo;
        entity.RestartRequired = restartRequired;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        entity.UpdatedBy = update.ChangedBy;

        await protectedCredentialService.UpdateAsync(update.BrokerEnvironment, update.ApiKey, update.Identifier, update.Password, update.ChangedBy, cancellationToken).ConfigureAwait(false);

        var correlationId = Guid.NewGuid().ToString("N");

        dbContext.ConfigurationAudits.Add(new ConfigurationAuditEntity
        {
            ConfigurationId = entity.ConfigurationId,
            PlatformEnvironment = update.PlatformEnvironment.ToString(),
            BrokerEnvironment = update.BrokerEnvironment.ToString(),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            ChangedBy = update.ChangedBy,
            ChangeType = "PlatformConfigurationUpdated",
            Summary = restartRequired
                ? "Platform configuration updated. Startup-fixed changes will apply on next restart."
                : "Platform configuration updated.",
            DetailsJson = OperationalDataRedactor.Serialize(new
            {
                update.PlatformEnvironment,
                update.BrokerEnvironment,
                update.TradingSchedule.StartOfDay,
                update.TradingSchedule.EndOfDay,
                update.TradingSchedule.TradingDays,
                update.TradingSchedule.WeekendBehavior,
                update.TradingSchedule.BankHolidayExclusions,
                update.RetryPolicy.InitialDelaySeconds,
                update.RetryPolicy.MaxAutomaticRetries,
                update.RetryPolicy.PeriodicDelayMinutes,
                update.NotificationSettings.Provider,
                update.NotificationSettings.EmailTo,
                SecretsUpdated = new
                {
                    ApiKey = !string.IsNullOrWhiteSpace(update.ApiKey),
                    Identifier = !string.IsNullOrWhiteSpace(update.Identifier),
                    Password = !string.IsNullOrWhiteSpace(update.Password)
                }
            }),
            CorrelationId = correlationId
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        return new UpdatePlatformConfigurationResult(snapshot, restartRequired);
    }

    private async Task<PlatformConfigurationEntity> EnsureConfigurationAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.PlatformConfigurations.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            return entity;
        }

        var bootstrap = PlatformConfigurationBootstrapParser.Parse(configuration);

        entity = new PlatformConfigurationEntity
        {
            PlatformEnvironment = bootstrap.PlatformEnvironment.ToString(),
            BrokerEnvironment = bootstrap.BrokerEnvironment.ToString(),
            TradingHoursStart = bootstrap.TradingSchedule.StartOfDay,
            TradingHoursEnd = bootstrap.TradingSchedule.EndOfDay,
            TradingDaysCsv = string.Join(',', bootstrap.TradingSchedule.TradingDays),
            WeekendBehavior = bootstrap.TradingSchedule.WeekendBehavior.ToString(),
            BankHolidayExclusionsJson = JsonSerializer.Serialize(bootstrap.TradingSchedule.BankHolidayExclusions),
            TimeZone = bootstrap.TradingSchedule.TimeZone,
            RetryInitialDelaySeconds = bootstrap.RetryPolicy.InitialDelaySeconds,
            RetryMaxAutomaticRetries = bootstrap.RetryPolicy.MaxAutomaticRetries,
            RetryMultiplier = bootstrap.RetryPolicy.Multiplier,
            RetryMaxDelaySeconds = bootstrap.RetryPolicy.MaxDelaySeconds,
            RetryPeriodicDelayMinutes = bootstrap.RetryPolicy.PeriodicDelayMinutes,
            NotificationProvider = bootstrap.NotificationSettings.Provider,
            NotificationEmailTo = bootstrap.NotificationSettings.EmailTo,
            UpdatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedBy = bootstrap.UpdatedBy,
            RestartRequired = false
        };

        dbContext.PlatformConfigurations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    private Task<PlatformConfigurationSnapshot> MapAsync(PlatformConfigurationEntity entity, CancellationToken cancellationToken)
    {
        var platformEnvironment = Enum.Parse<PlatformEnvironmentKind>(entity.PlatformEnvironment, ignoreCase: true);
        var brokerEnvironment = Enum.Parse<BrokerEnvironmentKind>(entity.BrokerEnvironment, ignoreCase: true);
        return MapAsync(entity, platformEnvironment, brokerEnvironment, cancellationToken);
    }

    private async Task<PlatformConfigurationSnapshot> MapAsync(
        PlatformConfigurationEntity entity,
        PlatformEnvironmentKind platformEnvironment,
        BrokerEnvironmentKind brokerEnvironment,
        CancellationToken cancellationToken)
    {
        var tradingDays = entity.TradingDaysCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.Parse<DayOfWeek>(value, ignoreCase: true))
            .ToArray();

        var bankHolidays = JsonSerializer.Deserialize<DateOnly[]>(entity.BankHolidayExclusionsJson) ?? [];
        var credentials = await protectedCredentialService.GetPresenceAsync(brokerEnvironment, cancellationToken).ConfigureAwait(false);

        return new PlatformConfigurationSnapshot(
            platformEnvironment,
            brokerEnvironment,
            new TradingScheduleConfiguration(
                entity.TradingHoursStart,
                entity.TradingHoursEnd,
                tradingDays,
                Enum.Parse<WeekendBehavior>(entity.WeekendBehavior, ignoreCase: true),
                bankHolidays,
                entity.TimeZone),
            new RetryPolicyConfiguration(
                entity.RetryInitialDelaySeconds,
                entity.RetryMaxAutomaticRetries,
                entity.RetryMultiplier,
                entity.RetryMaxDelaySeconds,
                entity.RetryPeriodicDelayMinutes),
            new NotificationSettingsConfiguration(entity.NotificationProvider, entity.NotificationEmailTo),
            credentials,
            LiveOptionVisible: true,
            LiveOptionAvailable: platformEnvironment != PlatformEnvironmentKind.Test,
            entity.UpdatedAtUtc,
            entity.RestartRequired);
    }
}
