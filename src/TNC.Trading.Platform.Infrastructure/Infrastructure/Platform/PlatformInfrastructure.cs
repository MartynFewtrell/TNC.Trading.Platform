using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Persistence;
using AppNotificationDispatcher = TNC.Trading.Platform.Application.Services.INotificationDispatcher;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal static class PlatformInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("platformdb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseInMemoryDatabase("tnc-trading-platform");
                return;
            }

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<ProtectedCredentialService>();
        services.AddScoped<IPlatformConfigurationStore, SqlPlatformConfigurationStore>();
        services.AddScoped<IPlatformRuntimeStateStore, EfPlatformRuntimeStateStore>();
        services.AddScoped<IPlatformRetryCycleStore, EfPlatformRetryCycleStore>();
        services.AddScoped<IPlatformEventStore, EfPlatformEventStore>();
        services.AddScoped<INotificationProvider, RecordedNotificationProvider>();
        services.AddScoped<INotificationProvider, SmtpNotificationProvider>();
        services.AddScoped<INotificationProvider, AzureCommunicationServicesEmailNotificationProvider>();
        services.AddScoped<AppNotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<OperationalRecordRetentionProcessor>();
        services.AddHostedService<OperationalRecordRetentionService>();

        return services;
    }
}

internal sealed class ProtectedCredentialService(
    PlatformDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Platform.IgCredentials");

    public async Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var credentialTypes = await dbContext.ProtectedCredentials
            .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString())
            .Select(item => item.CredentialType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CredentialPresence(
            credentialTypes.Contains("ApiKey", StringComparer.Ordinal),
            credentialTypes.Contains("Identifier", StringComparer.Ordinal),
            credentialTypes.Contains("Password", StringComparer.Ordinal));
    }

    public async Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            await UpsertCredentialAsync(brokerEnvironment, "ApiKey", apiKey, changedBy, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            await UpsertCredentialAsync(brokerEnvironment, "Identifier", identifier, changedBy, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            await UpsertCredentialAsync(brokerEnvironment, "Password", password, changedBy, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UpsertCredentialAsync(BrokerEnvironmentKind brokerEnvironment, string credentialType, string secret, string changedBy, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ProtectedCredentials
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironment == brokerEnvironment.ToString() && item.CredentialType == credentialType,
                cancellationToken)
            .ConfigureAwait(false);

        entity ??= new ProtectedCredentialEntity
        {
            BrokerEnvironment = brokerEnvironment.ToString(),
            CredentialType = credentialType
        };

        entity.ProtectedValue = protector.Protect(secret);
        entity.ProtectionKind = "DataProtection";
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        entity.UpdatedBy = changedBy;

        if (entity.CredentialId == 0)
        {
            dbContext.ProtectedCredentials.Add(entity);
        }
    }
}

internal sealed class SqlPlatformConfigurationStore(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    ProtectedCredentialService protectedCredentialService,
    TimeProvider timeProvider) : IPlatformConfigurationStore
{
    public async Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken).ConfigureAwait(false);
        return await MapAsync(entity, cancellationToken).ConfigureAwait(false);
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
            NotificationProvider = configuration["Bootstrap:NotificationSettings:Provider"] ?? "RecordedOnly",
            NotificationEmailTo = configuration["Bootstrap:NotificationSettings:EmailTo"],
            UpdatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedBy = updatedBy,
            RestartRequired = false
        };

        dbContext.PlatformConfigurations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
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

    private async Task<PlatformConfigurationSnapshot> MapAsync(PlatformConfigurationEntity entity, CancellationToken cancellationToken)
    {
        var tradingDays = entity.TradingDaysCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.Parse<DayOfWeek>(value, ignoreCase: true))
            .ToArray();

        var bankHolidays = JsonSerializer.Deserialize<DateOnly[]>(entity.BankHolidayExclusionsJson) ?? [];
        var platformEnvironment = Enum.Parse<PlatformEnvironmentKind>(entity.PlatformEnvironment, ignoreCase: true);
        var brokerEnvironment = Enum.Parse<BrokerEnvironmentKind>(entity.BrokerEnvironment, ignoreCase: true);
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

internal sealed class EfPlatformRuntimeStateStore(PlatformDbContext dbContext) : IPlatformRuntimeStateStore
{
    public async Task<PlatformRuntimeState> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.AuthRuntimeStates.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new AuthRuntimeStateEntity
            {
                TradingScheduleStatus = "Inactive",
                SessionStatus = PlatformSessionStatus.Unknown.ToString(),
                IsDegraded = false,
                RetryPhase = AuthRetryPhase.None.ToString(),
                AutomaticAttemptNumber = 0,
                RetryLimitReached = false
            };

            dbContext.AuthRuntimeStates.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Map(entity);
    }

    public async Task SaveAsync(PlatformRuntimeState state, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AuthRuntimeStates.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new AuthRuntimeStateEntity();
            dbContext.AuthRuntimeStates.Add(entity);
        }

        entity.PlatformEnvironment = state.PlatformEnvironment;
        entity.BrokerEnvironment = state.BrokerEnvironment;
        entity.TradingScheduleStatus = state.TradingScheduleStatus;
        entity.SessionStatus = state.SessionStatus.ToString();
        entity.IsDegraded = state.IsDegraded;
        entity.BlockedReason = state.BlockedReason;
        entity.RetryPhase = state.RetryPhase.ToString();
        entity.AutomaticAttemptNumber = state.AutomaticAttemptNumber;
        entity.NextRetryAtUtc = state.NextRetryAtUtc;
        entity.RetryLimitReached = state.RetryLimitReached;
        entity.CurrentRetryCycleId = state.CurrentRetryCycleId;
        entity.EstablishedAtUtc = state.EstablishedAtUtc;
        entity.ExpiresAtUtc = state.ExpiresAtUtc;
        entity.LastValidatedAtUtc = state.LastValidatedAtUtc;
        entity.LastTransitionAtUtc = state.LastTransitionAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static PlatformRuntimeState Map(AuthRuntimeStateEntity entity)
    {
        return new PlatformRuntimeState
        {
            PlatformEnvironment = entity.PlatformEnvironment,
            BrokerEnvironment = entity.BrokerEnvironment,
            TradingScheduleStatus = entity.TradingScheduleStatus,
            SessionStatus = Enum.Parse<PlatformSessionStatus>(entity.SessionStatus, ignoreCase: true),
            IsDegraded = entity.IsDegraded,
            BlockedReason = entity.BlockedReason,
            RetryPhase = Enum.Parse<AuthRetryPhase>(entity.RetryPhase, ignoreCase: true),
            AutomaticAttemptNumber = entity.AutomaticAttemptNumber,
            NextRetryAtUtc = entity.NextRetryAtUtc,
            RetryLimitReached = entity.RetryLimitReached,
            CurrentRetryCycleId = entity.CurrentRetryCycleId,
            EstablishedAtUtc = entity.EstablishedAtUtc,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            LastValidatedAtUtc = entity.LastValidatedAtUtc,
            LastTransitionAtUtc = entity.LastTransitionAtUtc
        };
    }
}

internal sealed class EfPlatformRetryCycleStore(PlatformDbContext dbContext) : IPlatformRetryCycleStore
{
    public async Task UpsertAsync(PlatformRetryCycle cycle, CancellationToken cancellationToken)
    {
        var entity = dbContext.AuthRetryCycles.Local
            .FirstOrDefault(item => item.RetryCycleId == cycle.RetryCycleId);

        entity ??= await dbContext.AuthRetryCycles
            .SingleOrDefaultAsync(item => item.RetryCycleId == cycle.RetryCycleId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new AuthRetryCycleEntity
            {
                RetryCycleId = cycle.RetryCycleId,
                StartedAtUtc = cycle.StartedAtUtc,
                CycleType = cycle.CycleType
            };

            dbContext.AuthRetryCycles.Add(entity);
        }

        entity.CycleType = cycle.CycleType;
        entity.PlatformEnvironment = cycle.PlatformEnvironment;
        entity.BrokerEnvironment = cycle.BrokerEnvironment;
        entity.RetryPhase = cycle.RetryPhase.ToString();
        entity.AutomaticAttemptNumber = cycle.AutomaticAttemptNumber;
        entity.NextRetryAtUtc = cycle.NextRetryAtUtc;
        entity.LastDelaySeconds = cycle.LastDelaySeconds;
        entity.PeriodicDelayMinutes = cycle.PeriodicDelayMinutes;
        entity.MaxAutomaticRetries = cycle.MaxAutomaticRetries;
        entity.RetryLimitReached = cycle.RetryLimitReached;
        entity.FailureNotificationSent = cycle.FailureNotificationSent || entity.FailureNotificationSent;
        entity.UpdatedAtUtc = cycle.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class EfPlatformEventStore(PlatformDbContext dbContext) : IPlatformEventStore
{
    public async Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken)
    {
        var query = dbContext.OperationalEvents.AsNoTracking().OrderByDescending(item => item.OccurredAtUtc).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(item => item.BrokerEnvironment == environment);
        }

        var events = await query.Take(50).ToListAsync(cancellationToken).ConfigureAwait(false);
        return events
            .Select(item => new OperationalEventModel(
                item.EventId,
                item.Category,
                item.EventType,
                Enum.Parse<PlatformEnvironmentKind>(item.PlatformEnvironment, ignoreCase: true),
                Enum.Parse<BrokerEnvironmentKind>(item.BrokerEnvironment, ignoreCase: true),
                item.Summary,
                item.DetailsJson,
                item.OccurredAtUtc))
            .ToArray();
    }

    public async Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken)
    {
        dbContext.OperationalEvents.Add(new OperationalEventEntity
        {
            Category = record.Category,
            EventType = record.EventType,
            PlatformEnvironment = record.PlatformEnvironment.ToString(),
            BrokerEnvironment = record.BrokerEnvironment.ToString(),
            Severity = record.Severity,
            Summary = record.Summary,
            DetailsJson = OperationalDataRedactor.Serialize(record.Details),
            CorrelationId = record.CorrelationId,
            RetryCycleId = record.RetryCycleId,
            OccurredAtUtc = record.OccurredAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class NotificationDispatcher(
    PlatformDbContext dbContext,
    IEnumerable<INotificationProvider> notificationProviders,
    ILogger<NotificationDispatcher> logger,
    TimeProvider timeProvider) : AppNotificationDispatcher
{
    public Task DispatchFailureAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("AuthFailure", summary, configuration, correlationId, retryCycleId, cancellationToken);

    public Task DispatchRetryLimitReachedAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("RetryLimitReached", summary, configuration, correlationId, retryCycleId, cancellationToken);

    public Task DispatchRecoveryAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("AuthRecovered", summary, configuration, correlationId, retryCycleId, cancellationToken);

    public Task DispatchBlockedLiveAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("BlockedLiveAttempt", summary, configuration, correlationId, retryCycleId, cancellationToken);

    private async Task RecordAsync(
        string notificationType,
        string summary,
        PlatformConfigurationSnapshot configuration,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        var recipient = string.IsNullOrWhiteSpace(configuration.NotificationSettings.EmailTo)
            ? "unconfigured"
            : configuration.NotificationSettings.EmailTo!;

        var providerName = configuration.NotificationSettings.Provider;
        var dispatchResult = await DispatchAsync(
            new NotificationMessage(notificationType, recipient, summary),
            providerName,
            cancellationToken).ConfigureAwait(false);

        dbContext.NotificationRecords.Add(new NotificationRecordEntity
        {
            NotificationType = notificationType,
            PlatformEnvironment = configuration.PlatformEnvironment.ToString(),
            BrokerEnvironment = configuration.BrokerEnvironment.ToString(),
            Recipient = recipient,
            Summary = summary,
            DispatchStatus = dispatchResult.Status,
            Provider = dispatchResult.ProviderName,
            CorrelationId = correlationId,
            RetryCycleId = retryCycleId,
            DispatchedAtUtc = timeProvider.GetUtcNow()
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Notification {DispatchStatus} for {NotificationType} to {Recipient}: {Summary}",
            dispatchResult.Status,
            notificationType,
            recipient,
            summary);
    }

    private async Task<NotificationDispatchResult> DispatchAsync(
        NotificationMessage message,
        string providerName,
        CancellationToken cancellationToken)
    {
        if (string.Equals(message.Recipient, "unconfigured", StringComparison.Ordinal))
        {
            return new NotificationDispatchResult("Skipped", "Notification recipient is not configured.", providerName);
        }

        var provider = notificationProviders.SingleOrDefault(item =>
            string.Equals(item.Name, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return new NotificationDispatchResult("Failed", $"Notification provider '{providerName}' is not registered.", providerName);
        }

        try
        {
            return await provider.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Net.Mail.SmtpException or Azure.RequestFailedException)
        {
            var sanitizedMessage = OperationalDataRedactor.RedactText(exception.Message) ?? "Notification dispatch failed.";
            logger.LogError(
                "Notification provider {ProviderName} failed for {EventType}: {ErrorMessage}",
                providerName,
                message.EventType,
                sanitizedMessage);

            return new NotificationDispatchResult("Failed", sanitizedMessage, provider.Name);
        }
    }
}

internal sealed class OperationalRecordRetentionProcessor(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<OperationalRecordRetentionProcessor> logger)
{
    public async Task<int> ApplyAsync(CancellationToken cancellationToken)
    {
        var retentionDays = GetRetentionDays();
        var cutoff = timeProvider.GetUtcNow().AddDays(-retentionDays);

        var expiredOperationalEvents = await dbContext.OperationalEvents
            .Where(item => item.OccurredAtUtc < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var expiredConfigurationAudits = await dbContext.ConfigurationAudits
            .Where(item => item.OccurredAtUtc < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var expiredNotificationRecords = await dbContext.NotificationRecords
            .Where(item => item.DispatchedAtUtc < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedCount = expiredOperationalEvents.Count + expiredConfigurationAudits.Count + expiredNotificationRecords.Count;
        if (deletedCount == 0)
        {
            return 0;
        }

        dbContext.OperationalEvents.RemoveRange(expiredOperationalEvents);
        dbContext.ConfigurationAudits.RemoveRange(expiredConfigurationAudits);
        dbContext.NotificationRecords.RemoveRange(expiredNotificationRecords);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Deleted {DeletedCount} expired operational records using a retention window of {RetentionDays} days.",
            deletedCount,
            retentionDays);

        return deletedCount;
    }

    private int GetRetentionDays()
    {
        var configuredValue = configuration["Retention:OperationalRecordsDays"];
        return int.TryParse(configuredValue, out var retentionDays) && retentionDays > 0
            ? retentionDays
            : 90;
    }
}

internal sealed class OperationalRecordRetentionService(IServiceScopeFactory serviceScopeFactory, ILogger<OperationalRecordRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OperationalRecordRetentionProcessor>();
                await processor.ApplyAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Operational record retention failed: {ErrorMessage}",
                    OperationalDataRedactor.RedactText(exception.Message) ?? "Unhandled failure.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
