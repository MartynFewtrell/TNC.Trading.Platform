using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class SqlPlatformConfigurationStore(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    ProtectedCredentialService protectedCredentialService,
    TimeProvider timeProvider) : IPlatformConfigurationStore
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

    public async Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var restartRequired = entity.PlatformEnvironment != update.PlatformEnvironment.ToString()
            || entity.BrokerEnvironment != update.BrokerEnvironment.ToString();

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

        var bootstrapBrokerEnvironment = configuration["Bootstrap:BrokerEnvironment"];
        if (string.IsNullOrWhiteSpace(bootstrapBrokerEnvironment))
        {
            throw new InvalidOperationException("Bootstrap:BrokerEnvironment must be configured before the platform can seed SQL-backed configuration.");
        }

        var bootstrapPlatformEnvironment = configuration["Bootstrap:PlatformEnvironment"];
        var platformEnvironment = string.IsNullOrWhiteSpace(bootstrapPlatformEnvironment)
            ? PlatformEnvironmentKind.Test
            : Enum.Parse<PlatformEnvironmentKind>(bootstrapPlatformEnvironment, ignoreCase: true);
        var brokerEnvironment = Enum.Parse<BrokerEnvironmentKind>(bootstrapBrokerEnvironment, ignoreCase: true);
        var tradingDays = GetTradingDays();
        var bankHolidays = GetBankHolidayExclusions();
        var updatedBy = configuration["Bootstrap:UpdatedBy"] ?? "bootstrap";

        entity = new PlatformConfigurationEntity
        {
            PlatformEnvironment = platformEnvironment.ToString(),
            BrokerEnvironment = brokerEnvironment.ToString(),
            TradingHoursStart = GetTimeOnly("Bootstrap:TradingSchedule:StartOfDay", new TimeOnly(8, 0)),
            TradingHoursEnd = GetTimeOnly("Bootstrap:TradingSchedule:EndOfDay", new TimeOnly(16, 30)),
            TradingDaysCsv = string.Join(',', tradingDays),
            WeekendBehavior = GetWeekendBehavior().ToString(),
            BankHolidayExclusionsJson = JsonSerializer.Serialize(bankHolidays),
            TimeZone = configuration["Bootstrap:TradingSchedule:TimeZone"] ?? "UTC",
            RetryInitialDelaySeconds = GetInt32("Bootstrap:RetryPolicy:InitialDelaySeconds", 1),
            RetryMaxAutomaticRetries = GetInt32("Bootstrap:RetryPolicy:MaxAutomaticRetries", 5),
            RetryMultiplier = GetInt32("Bootstrap:RetryPolicy:Multiplier", 2),
            RetryMaxDelaySeconds = GetInt32("Bootstrap:RetryPolicy:MaxDelaySeconds", 60),
            RetryPeriodicDelayMinutes = GetInt32("Bootstrap:RetryPolicy:PeriodicDelayMinutes", 5),
            NotificationProvider = GetNotificationProvider(),
            NotificationEmailTo = configuration["Bootstrap:NotificationSettings:EmailTo"],
            UpdatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedBy = updatedBy,
            RestartRequired = false
        };

        dbContext.PlatformConfigurations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    private string GetNotificationProvider()
    {
        var configuredProvider = configuration["Bootstrap:NotificationSettings:Provider"];
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return configuredProvider;
        }

        return string.IsNullOrWhiteSpace(configuration["NotificationTransports:Smtp:Host"])
            ? "RecordedOnly"
            : "Smtp";
    }

    private IReadOnlyList<DayOfWeek> GetTradingDays()
    {
        var configuredDays = configuration.GetSection("Bootstrap:TradingSchedule:TradingDays").Get<string[]>();
        if (configuredDays is { Length: > 0 })
        {
            return configuredDays
                .Select(value => Enum.Parse<DayOfWeek>(value, ignoreCase: true))
                .ToArray();
        }

        return
        [
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        ];
    }

    private IReadOnlyList<DateOnly> GetBankHolidayExclusions()
    {
        var configuredDates = configuration.GetSection("Bootstrap:TradingSchedule:BankHolidayExclusions").Get<string[]>();
        if (configuredDates is not { Length: > 0 })
        {
            return [];
        }

        return configuredDates
            .Select(DateOnly.Parse)
            .ToArray();
    }

    private WeekendBehavior GetWeekendBehavior()
    {
        var configuredWeekendBehavior = configuration["Bootstrap:TradingSchedule:WeekendBehavior"];
        return string.IsNullOrWhiteSpace(configuredWeekendBehavior)
            ? WeekendBehavior.ExcludeWeekends
            : Enum.Parse<WeekendBehavior>(configuredWeekendBehavior, ignoreCase: true);
    }

    private int GetInt32(string key, int defaultValue)
    {
        var configuredValue = configuration[key];
        return string.IsNullOrWhiteSpace(configuredValue) ? defaultValue : int.Parse(configuredValue);
    }

    private TimeOnly GetTimeOnly(string key, TimeOnly defaultValue)
    {
        var configuredValue = configuration[key];
        return string.IsNullOrWhiteSpace(configuredValue) ? defaultValue : TimeOnly.Parse(configuredValue);
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
