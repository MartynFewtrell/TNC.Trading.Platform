using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Api.Configuration;
using TNC.Trading.Platform.Api.Infrastructure.Ig;
using TNC.Trading.Platform.Api.Infrastructure.Notifications;
using TNC.Trading.Platform.Api.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Api.Infrastructure.Platform;

internal sealed class PlatformValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Platform validation failed")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

internal sealed record PlatformConfigurationUpdate(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    TradingScheduleConfiguration TradingSchedule,
    RetryPolicyConfiguration RetryPolicy,
    NotificationSettingsConfiguration NotificationSettings,
    string? ApiKey,
    string? Identifier,
    string? Password,
    string ChangedBy);

internal sealed record UpdatePlatformConfigurationResult(
    PlatformConfigurationSnapshot Snapshot,
    bool RestartRequired);

internal sealed record ManualRetryResult(Guid RetryCycleId);

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
            .ToListAsync(cancellationToken);

        return new CredentialPresence(
            credentialTypes.Contains("ApiKey", StringComparer.Ordinal),
            credentialTypes.Contains("Identifier", StringComparer.Ordinal),
            credentialTypes.Contains("Password", StringComparer.Ordinal));
    }

    public async Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            await UpsertCredentialAsync(brokerEnvironment, "ApiKey", apiKey, changedBy, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            await UpsertCredentialAsync(brokerEnvironment, "Identifier", identifier, changedBy, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            await UpsertCredentialAsync(brokerEnvironment, "Password", password, changedBy, cancellationToken);
        }
    }

    private async Task UpsertCredentialAsync(BrokerEnvironmentKind brokerEnvironment, string credentialType, string secret, string changedBy, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ProtectedCredentials
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironment == brokerEnvironment.ToString() && item.CredentialType == credentialType,
                cancellationToken);

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

internal sealed class TradingScheduleGate
{
    public TradingScheduleStatus Evaluate(TradingScheduleConfiguration tradingSchedule, DateTimeOffset utcNow)
    {
        var timeZone = ResolveTimeZone(tradingSchedule.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var currentDate = DateOnly.FromDateTime(localNow.DateTime);

        if (tradingSchedule.BankHolidayExclusions.Contains(currentDate))
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the configured bank holiday.");
        }

        if (!tradingSchedule.TradingDays.Contains(localNow.DayOfWeek))
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the current day.");
        }

        var currentTime = TimeOnly.FromDateTime(localNow.DateTime);
        if (currentTime < tradingSchedule.StartOfDay || currentTime >= tradingSchedule.EndOfDay)
        {
            return new TradingScheduleStatus(false, "Trading schedule is inactive for the current time window.");
        }

        return new TradingScheduleStatus(true, "Trading schedule is active.");
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredTimeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

internal sealed class NotificationDispatcher(
    PlatformDbContext dbContext,
    IEnumerable<INotificationProvider> notificationProviders,
    ILogger<NotificationDispatcher> logger,
    TimeProvider timeProvider)
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
            cancellationToken);

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
            return await provider.DispatchAsync(message, cancellationToken);
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
            .ToListAsync(cancellationToken);
        var expiredConfigurationAudits = await dbContext.ConfigurationAudits
            .Where(item => item.OccurredAtUtc < cutoff)
            .ToListAsync(cancellationToken);
        var expiredNotificationRecords = await dbContext.NotificationRecords
            .Where(item => item.DispatchedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        var deletedCount = expiredOperationalEvents.Count + expiredConfigurationAudits.Count + expiredNotificationRecords.Count;
        if (deletedCount == 0)
        {
            return 0;
        }

        dbContext.OperationalEvents.RemoveRange(expiredOperationalEvents);
        dbContext.ConfigurationAudits.RemoveRange(expiredConfigurationAudits);
        dbContext.NotificationRecords.RemoveRange(expiredNotificationRecords);
        await dbContext.SaveChangesAsync(cancellationToken);

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

internal sealed class PlatformConfigurationService(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    ProtectedCredentialService protectedCredentialService,
    TimeProvider timeProvider)
{
    public async Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken)
    {
        var entity = await EnsureConfigurationAsync(cancellationToken);
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

        await protectedCredentialService.UpdateAsync(update.BrokerEnvironment, update.ApiKey, update.Identifier, update.Password, update.ChangedBy, cancellationToken);

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

        await dbContext.SaveChangesAsync(cancellationToken);

        var snapshot = await MapAsync(entity, cancellationToken);
        return new UpdatePlatformConfigurationResult(snapshot, restartRequired);
    }

    private async Task<PlatformConfigurationEntity> EnsureConfigurationAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.PlatformConfigurations.SingleOrDefaultAsync(cancellationToken);
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
        await dbContext.SaveChangesAsync(cancellationToken);
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
        var credentials = await protectedCredentialService.GetPresenceAsync(brokerEnvironment, cancellationToken);

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

internal sealed class PlatformStateCoordinator(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    PlatformConfigurationService platformConfigurationService,
    TradingScheduleGate tradingScheduleGate,
    NotificationDispatcher notificationDispatcher,
    TimeProvider timeProvider,
    ILogger<PlatformStateCoordinator> logger)
{
    public async Task<PlatformStatusModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        await TickAsync(cancellationToken);

        var configuration = await platformConfigurationService.GetCurrentAsync(cancellationToken);
        var state = await GetOrCreateStateAsync(cancellationToken);
        var scheduleStatus = tradingScheduleGate.Evaluate(configuration.TradingSchedule, timeProvider.GetUtcNow());
        ApplyRuntimeContext(configuration, state, scheduleStatus);

        return new PlatformStatusModel(
            configuration.PlatformEnvironment,
            configuration.BrokerEnvironment,
            configuration.LiveOptionVisible,
            configuration.LiveOptionAvailable,
            configuration.TradingSchedule,
            scheduleStatus,
            Enum.Parse<PlatformSessionStatus>(state.SessionStatus, ignoreCase: true),
            state.IsDegraded,
            state.BlockedReason,
            new PlatformRetryState(
                Enum.Parse<AuthRetryPhase>(state.RetryPhase, ignoreCase: true),
                state.AutomaticAttemptNumber,
                state.NextRetryAtUtc,
                state.RetryLimitReached,
                state.RetryLimitReached && scheduleStatus.IsActive && Enum.Parse<PlatformSessionStatus>(state.SessionStatus, ignoreCase: true) == PlatformSessionStatus.Degraded),
            state.LastTransitionAtUtc ?? configuration.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken)
    {
        await TickAsync(cancellationToken);

        var query = dbContext.OperationalEvents.AsNoTracking().OrderByDescending(item => item.OccurredAtUtc).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(item => item.BrokerEnvironment == environment);
        }

        var events = await query.Take(50).ToListAsync(cancellationToken);
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

    public async Task<ManualRetryResult> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        var configuration = await platformConfigurationService.GetCurrentAsync(cancellationToken);
        var scheduleStatus = tradingScheduleGate.Evaluate(configuration.TradingSchedule, timeProvider.GetUtcNow());
        var state = await GetOrCreateStateAsync(cancellationToken);
        ApplyRuntimeContext(configuration, state, scheduleStatus);

        if (!scheduleStatus.IsActive)
        {
            throw new InvalidOperationException("Manual retry is unavailable while the trading schedule is inactive.");
        }

        if (configuration.PlatformEnvironment == PlatformEnvironmentKind.Test && configuration.BrokerEnvironment == BrokerEnvironmentKind.Live)
        {
            await HandleBlockedLiveAsync(configuration, state, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("IG live is unavailable while the platform environment is Test.");
        }

        if (!state.RetryLimitReached || !string.Equals(state.SessionStatus, PlatformSessionStatus.Degraded.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Manual retry becomes available only after the initial automatic retries are exhausted.");
        }

        var now = timeProvider.GetUtcNow();
        var cycleId = Guid.NewGuid();

        state.CurrentRetryCycleId = cycleId;
        state.AutomaticAttemptNumber = 0;
        state.RetryPhase = AuthRetryPhase.InitialAutomatic.ToString();
        state.RetryLimitReached = false;
        state.NextRetryAtUtc = now.AddSeconds(GetDelayBeforeAttempt(configuration.RetryPolicy, 1));
        state.SessionStatus = PlatformSessionStatus.Degraded.ToString();
        state.IsDegraded = true;
        state.BlockedReason = "IG demo credentials are incomplete.";
        state.EstablishedAtUtc = null;
        state.LastValidatedAtUtc = now;
        state.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(cycleId, configuration, state, "Manual", failureNotificationSent: false, GetDelayBeforeAttempt(configuration.RetryPolicy, 1), cancellationToken);

        var correlationId = CreateCorrelationId();

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            "ManualRetryRequested",
            "Manual retry requested for the current degraded auth cycle.",
            new { RetryCycleId = cycleId },
            "Information",
            correlationId,
            cycleId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await AttemptImmediateRecoveryAsync(configuration, state, "Manual", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ManualRetryResult(cycleId);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var configuration = await platformConfigurationService.GetCurrentAsync(cancellationToken);
        var state = await GetOrCreateStateAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var scheduleStatus = tradingScheduleGate.Evaluate(configuration.TradingSchedule, now);
        ApplyRuntimeContext(configuration, state, scheduleStatus);

        if (!scheduleStatus.IsActive)
        {
            await TransitionToOutOfScheduleAsync(configuration, state, scheduleStatus.Reason, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (configuration.PlatformEnvironment == PlatformEnvironmentKind.Test && configuration.BrokerEnvironment == BrokerEnvironmentKind.Live)
        {
            await HandleBlockedLiveAsync(configuration, state, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (HasSessionExpired(state, now))
        {
            await HandleSessionExpiredAsync(configuration, state, cancellationToken);
        }

        if (configuration.Credentials.IsComplete)
        {
            await TransitionToActiveAsync(configuration, state, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await TransitionToDegradedAsync(configuration, state, cancellationToken);

        if (state.NextRetryAtUtc is not null && state.NextRetryAtUtc <= now)
        {
            if (Enum.Parse<AuthRetryPhase>(state.RetryPhase, ignoreCase: true) == AuthRetryPhase.InitialAutomatic)
            {
                state.AutomaticAttemptNumber += 1;
                if (state.AutomaticAttemptNumber >= configuration.RetryPolicy.MaxAutomaticRetries)
                {
                    state.RetryPhase = AuthRetryPhase.Periodic.ToString();
                    state.RetryLimitReached = true;
                    state.NextRetryAtUtc = now.AddMinutes(configuration.RetryPolicy.PeriodicDelayMinutes);
                    state.LastTransitionAtUtc = now;

                    var lastDelay = GetDelayBeforeAttempt(configuration.RetryPolicy, state.AutomaticAttemptNumber);
                    var summary = $"Initial automatic IG demo auth retries are exhausted after {state.AutomaticAttemptNumber} attempts. Periodic retry continues every {configuration.RetryPolicy.PeriodicDelayMinutes} minutes.";
                    var correlationId = CreateCorrelationId();

                    await UpsertRetryCycleAsync(state.CurrentRetryCycleId, configuration, state, "Automatic", failureNotificationSent: true, lastDelay, cancellationToken);
                    await WriteOperationalEventAsync(
                        configuration,
                        "auth",
                        "RetryLimitReached",
                        summary,
                        new
                        {
                            state.AutomaticAttemptNumber,
                            LastScheduledDelaySeconds = lastDelay,
                            configuration.RetryPolicy.PeriodicDelayMinutes
                        },
                        "Warning",
                        correlationId,
                        state.CurrentRetryCycleId,
                        cancellationToken);
                    await notificationDispatcher.DispatchRetryLimitReachedAsync(configuration, summary, correlationId, state.CurrentRetryCycleId, cancellationToken);
                }
                else
                {
                    var nextDelay = GetDelayBeforeAttempt(configuration.RetryPolicy, state.AutomaticAttemptNumber + 1);
                    state.NextRetryAtUtc = now.AddSeconds(nextDelay);
                    state.LastTransitionAtUtc = now;

                    await UpsertRetryCycleAsync(state.CurrentRetryCycleId, configuration, state, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken);

                    await WriteOperationalEventAsync(
                        configuration,
                        "auth",
                        "RetryScheduled",
                        $"Automatic retry {state.AutomaticAttemptNumber} failed. Next retry is scheduled at {state.NextRetryAtUtc:O}.",
                        new
                        {
                            state.AutomaticAttemptNumber,
                            state.NextRetryAtUtc
                        },
                        "Information",
                        CreateCorrelationId(),
                        state.CurrentRetryCycleId,
                        cancellationToken);
                }
            }
            else
            {
                state.NextRetryAtUtc = now.AddMinutes(configuration.RetryPolicy.PeriodicDelayMinutes);
                state.LastTransitionAtUtc = now;

                await UpsertRetryCycleAsync(state.CurrentRetryCycleId, configuration, state, "Automatic", failureNotificationSent: true, configuration.RetryPolicy.PeriodicDelayMinutes * 60, cancellationToken);

                await WriteOperationalEventAsync(
                    configuration,
                    "auth",
                    "PeriodicRetryScheduled",
                    $"Periodic retry remains active. Next retry is scheduled at {state.NextRetryAtUtc:O}.",
                    new { state.NextRetryAtUtc },
                    "Information",
                    CreateCorrelationId(),
                    state.CurrentRetryCycleId,
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AttemptImmediateRecoveryAsync(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, string cycleType, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var retryCycleId = state.CurrentRetryCycleId;

        if (configuration.Credentials.IsComplete)
        {
            state.SessionStatus = PlatformSessionStatus.Active.ToString();
            state.IsDegraded = false;
            state.BlockedReason = null;
            state.RetryPhase = AuthRetryPhase.None.ToString();
            state.AutomaticAttemptNumber = 0;
            state.NextRetryAtUtc = null;
            state.RetryLimitReached = false;
            state.EstablishedAtUtc = now;
            state.ExpiresAtUtc = now.Add(GetSessionLifetime());
            state.LastValidatedAtUtc = now;
            state.LastTransitionAtUtc = now;

            await UpsertRetryCycleAsync(retryCycleId, configuration, state, cycleType, failureNotificationSent: false, lastDelaySeconds: null, cancellationToken);

            var recoveryCorrelationId = CreateCorrelationId();

            await WriteOperationalEventAsync(
                configuration,
                "auth",
                "Recovered",
                "IG demo auth recovered after manual retry.",
                new { RetryCycleId = retryCycleId },
                "Information",
                recoveryCorrelationId,
                retryCycleId,
                cancellationToken);
            state.CurrentRetryCycleId = null;
            await notificationDispatcher.DispatchRecoveryAsync(configuration, "IG demo auth recovered after manual retry.", recoveryCorrelationId, retryCycleId, cancellationToken);
            return;
        }

        state.SessionStatus = PlatformSessionStatus.Degraded.ToString();
        state.IsDegraded = true;
        state.BlockedReason = "IG demo credentials are incomplete.";
        state.RetryPhase = AuthRetryPhase.InitialAutomatic.ToString();
        state.AutomaticAttemptNumber = 0;
        var nextDelay = GetDelayBeforeAttempt(configuration.RetryPolicy, 1);
        state.NextRetryAtUtc = now.AddSeconds(nextDelay);
        state.EstablishedAtUtc = null;
        state.ExpiresAtUtc = null;
        state.LastValidatedAtUtc = now;
        state.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(retryCycleId, configuration, state, cycleType, failureNotificationSent: true, nextDelay, cancellationToken);

        var failureCorrelationId = CreateCorrelationId();

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            "FailureDetected",
            "Manual retry started a new degraded auth cycle because required IG demo credentials are still missing.",
            new { RetryCycleId = retryCycleId },
            "Warning",
            failureCorrelationId,
            retryCycleId,
            cancellationToken);
        await notificationDispatcher.DispatchFailureAsync(configuration, "Manual retry started a new degraded auth cycle because required IG demo credentials are still missing.", failureCorrelationId, retryCycleId, cancellationToken);
    }

    private async Task TransitionToOutOfScheduleAsync(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, string reason, CancellationToken cancellationToken)
    {
        if (string.Equals(state.SessionStatus, PlatformSessionStatus.OutOfSchedule.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(state.BlockedReason, reason, StringComparison.Ordinal))
        {
            return;
        }

        var retryCycleId = state.CurrentRetryCycleId;
        state.SessionStatus = PlatformSessionStatus.OutOfSchedule.ToString();
        state.IsDegraded = false;
        state.BlockedReason = reason;
        state.RetryPhase = AuthRetryPhase.None.ToString();
        state.AutomaticAttemptNumber = 0;
        state.NextRetryAtUtc = null;
        state.RetryLimitReached = false;
        state.EstablishedAtUtc = null;
        state.ExpiresAtUtc = null;
        state.LastValidatedAtUtc = timeProvider.GetUtcNow();
        state.LastTransitionAtUtc = timeProvider.GetUtcNow();

        await UpsertRetryCycleAsync(retryCycleId, configuration, state, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken);

        var correlationId = CreateCorrelationId();

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            "TradingScheduleInactive",
            reason,
            new { TradingScheduleActive = false },
            "Information",
            correlationId,
            retryCycleId,
            cancellationToken);

        state.CurrentRetryCycleId = null;
    }

    private async Task HandleBlockedLiveAsync(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, CancellationToken cancellationToken)
    {
        const string blockedReason = "IG live is unavailable while the platform environment is Test.";
        if (string.Equals(state.SessionStatus, PlatformSessionStatus.Blocked.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(state.BlockedReason, blockedReason, StringComparison.Ordinal))
        {
            return;
        }

        var retryCycleId = state.CurrentRetryCycleId;
        state.SessionStatus = PlatformSessionStatus.Blocked.ToString();
        state.IsDegraded = true;
        state.BlockedReason = blockedReason;
        state.RetryPhase = AuthRetryPhase.None.ToString();
        state.AutomaticAttemptNumber = 0;
        state.NextRetryAtUtc = null;
        state.RetryLimitReached = false;
        state.EstablishedAtUtc = null;
        state.ExpiresAtUtc = null;
        state.LastValidatedAtUtc = timeProvider.GetUtcNow();
        state.LastTransitionAtUtc = timeProvider.GetUtcNow();

        await UpsertRetryCycleAsync(retryCycleId, configuration, state, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken);

        var correlationId = CreateCorrelationId();

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            "BlockedLiveAttempt",
            "A live broker action was blocked because the platform environment is Test.",
            new { configuration.PlatformEnvironment, configuration.BrokerEnvironment },
            "Warning",
            correlationId,
            retryCycleId,
            cancellationToken);
        state.CurrentRetryCycleId = null;
        await notificationDispatcher.DispatchBlockedLiveAsync(configuration, "A live broker action was blocked because the platform environment is Test.", correlationId, retryCycleId, cancellationToken);
    }

    private async Task TransitionToActiveAsync(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, CancellationToken cancellationToken)
    {
        if (string.Equals(state.SessionStatus, PlatformSessionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            state.LastValidatedAtUtc = timeProvider.GetUtcNow();
            state.ExpiresAtUtc ??= state.LastValidatedAtUtc.Value.Add(GetSessionLifetime());
            return;
        }

        var wasDegraded = state.IsDegraded;
        var retryCycleId = state.CurrentRetryCycleId;
        var sanitizedAuthResponse = IgAuthenticationResponseSanitizer.Sanitize(new IgAuthenticateResponse(
            "configured-demo-session",
            null,
            null,
            wasDegraded ? "cst-token" : null,
            wasDegraded ? "security-token" : null,
            new Dictionary<string, string?>
            {
                ["CST"] = wasDegraded ? "cst-token" : null,
                ["X-SECURITY-TOKEN"] = wasDegraded ? "security-token" : null,
                ["Version"] = "3"
            }));

        state.SessionStatus = PlatformSessionStatus.Active.ToString();
        state.IsDegraded = false;
        state.BlockedReason = null;
        state.RetryPhase = AuthRetryPhase.None.ToString();
        state.AutomaticAttemptNumber = 0;
        state.NextRetryAtUtc = null;
        state.RetryLimitReached = false;
        state.EstablishedAtUtc = timeProvider.GetUtcNow();
        state.ExpiresAtUtc = state.EstablishedAtUtc.Value.Add(GetSessionLifetime());
        state.LastValidatedAtUtc = state.EstablishedAtUtc;
        state.LastTransitionAtUtc = state.EstablishedAtUtc;

        await UpsertRetryCycleAsync(retryCycleId, configuration, state, "Automatic", failureNotificationSent: wasDegraded, lastDelaySeconds: null, cancellationToken);

        var eventType = wasDegraded ? "Recovered" : "Authenticated";
        var summary = wasDegraded
            ? "IG demo auth is healthy again."
            : "IG demo auth is active for the configured trading schedule.";

        var correlationId = CreateCorrelationId();

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            eventType,
            summary,
            new
            {
                SessionStatus = state.SessionStatus,
                SanitizedAuthResponse = sanitizedAuthResponse
            },
            "Information",
            correlationId,
            retryCycleId,
            cancellationToken);

        state.CurrentRetryCycleId = null;

        if (wasDegraded)
        {
            await notificationDispatcher.DispatchRecoveryAsync(configuration, summary, correlationId, retryCycleId, cancellationToken);
        }
    }

    private async Task TransitionToDegradedAsync(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, CancellationToken cancellationToken)
    {
        if (string.Equals(state.SessionStatus, PlatformSessionStatus.Degraded.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        state.SessionStatus = PlatformSessionStatus.Degraded.ToString();
        state.IsDegraded = true;
        state.BlockedReason = "IG demo credentials are incomplete.";
        state.RetryPhase = AuthRetryPhase.InitialAutomatic.ToString();
        state.AutomaticAttemptNumber = 0;
        var nextDelay = GetDelayBeforeAttempt(configuration.RetryPolicy, 1);
        state.NextRetryAtUtc = now.AddSeconds(nextDelay);
        state.RetryLimitReached = false;
        state.CurrentRetryCycleId = Guid.NewGuid();
        state.EstablishedAtUtc = null;
        state.ExpiresAtUtc = null;
        state.LastValidatedAtUtc = now;
        state.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(state.CurrentRetryCycleId, configuration, state, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken);

        var correlationId = CreateCorrelationId();

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            "FailureDetected",
            "IG demo auth is degraded because required credentials are incomplete.",
            new { RetryCycleId = state.CurrentRetryCycleId },
            "Warning",
            correlationId,
            state.CurrentRetryCycleId,
            cancellationToken);
        await notificationDispatcher.DispatchFailureAsync(configuration, "IG demo auth is degraded because required credentials are incomplete.", correlationId, state.CurrentRetryCycleId, cancellationToken);
    }

    private async Task<AuthRuntimeStateEntity> GetOrCreateStateAsync(CancellationToken cancellationToken)
    {
        var state = await dbContext.AuthRuntimeStates.SingleOrDefaultAsync(cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new AuthRuntimeStateEntity
        {
            TradingScheduleStatus = "Inactive",
            SessionStatus = PlatformSessionStatus.Unknown.ToString(),
            IsDegraded = false,
            RetryPhase = AuthRetryPhase.None.ToString(),
            AutomaticAttemptNumber = 0,
            RetryLimitReached = false
        };

        dbContext.AuthRuntimeStates.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken);
        return state;
    }

    private void ApplyRuntimeContext(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, TradingScheduleStatus scheduleStatus)
    {
        state.PlatformEnvironment = configuration.PlatformEnvironment.ToString();
        state.BrokerEnvironment = configuration.BrokerEnvironment.ToString();
        state.TradingScheduleStatus = scheduleStatus.IsActive ? "Active" : "Inactive";
    }

    private async Task HandleSessionExpiredAsync(PlatformConfigurationSnapshot configuration, AuthRuntimeStateEntity state, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var nextDelay = GetDelayBeforeAttempt(configuration.RetryPolicy, 1);
        var expiredAtUtc = state.ExpiresAtUtc;

        state.SessionStatus = PlatformSessionStatus.Degraded.ToString();
        state.IsDegraded = true;
        state.BlockedReason = "The active IG demo session expired and is being re-established.";
        state.RetryPhase = AuthRetryPhase.InitialAutomatic.ToString();
        state.AutomaticAttemptNumber = 0;
        state.NextRetryAtUtc = now.AddSeconds(nextDelay);
        state.RetryLimitReached = false;
        state.CurrentRetryCycleId = Guid.NewGuid();
        state.EstablishedAtUtc = null;
        state.ExpiresAtUtc = null;
        state.LastValidatedAtUtc = now;
        state.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(state.CurrentRetryCycleId, configuration, state, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken);

        var correlationId = CreateCorrelationId();
        var summary = "The active IG demo session expired and re-authentication started.";

        await WriteOperationalEventAsync(
            configuration,
            "auth",
            "SessionExpired",
            summary,
            new
            {
                RetryCycleId = state.CurrentRetryCycleId,
                expiredAtUtc
            },
            "Warning",
            correlationId,
            state.CurrentRetryCycleId,
            cancellationToken);

        await notificationDispatcher.DispatchFailureAsync(configuration, summary, correlationId, state.CurrentRetryCycleId, cancellationToken);
    }

    private bool HasSessionExpired(AuthRuntimeStateEntity state, DateTimeOffset now)
    {
        return string.Equals(state.SessionStatus, PlatformSessionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase)
            && state.ExpiresAtUtc is not null
            && state.ExpiresAtUtc <= now;
    }

    private async Task UpsertRetryCycleAsync(
        Guid? retryCycleId,
        PlatformConfigurationSnapshot configuration,
        AuthRuntimeStateEntity state,
        string cycleType,
        bool failureNotificationSent,
        int? lastDelaySeconds,
        CancellationToken cancellationToken)
    {
        if (retryCycleId is null)
        {
            return;
        }

        var cycle = dbContext.AuthRetryCycles.Local
            .FirstOrDefault(item => item.RetryCycleId == retryCycleId.Value);

        cycle ??= await dbContext.AuthRetryCycles
            .SingleOrDefaultAsync(item => item.RetryCycleId == retryCycleId.Value, cancellationToken);

        if (cycle is null)
        {
            cycle = new AuthRetryCycleEntity
            {
                RetryCycleId = retryCycleId.Value,
                CycleType = cycleType,
                StartedAtUtc = timeProvider.GetUtcNow()
            };

            dbContext.AuthRetryCycles.Add(cycle);
        }

        cycle.CycleType = cycleType;
        cycle.PlatformEnvironment = configuration.PlatformEnvironment.ToString();
        cycle.BrokerEnvironment = configuration.BrokerEnvironment.ToString();
        cycle.RetryPhase = state.RetryPhase;
        cycle.AutomaticAttemptNumber = state.AutomaticAttemptNumber;
        cycle.NextRetryAtUtc = state.NextRetryAtUtc;
        cycle.LastDelaySeconds = lastDelaySeconds;
        cycle.PeriodicDelayMinutes = configuration.RetryPolicy.PeriodicDelayMinutes;
        cycle.MaxAutomaticRetries = configuration.RetryPolicy.MaxAutomaticRetries;
        cycle.RetryLimitReached = state.RetryLimitReached;
        cycle.FailureNotificationSent = failureNotificationSent || cycle.FailureNotificationSent;
        cycle.UpdatedAtUtc = timeProvider.GetUtcNow();
    }

    private Task WriteOperationalEventAsync(
        PlatformConfigurationSnapshot configuration,
        string category,
        string eventType,
        string summary,
        object details,
        string severity,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        dbContext.OperationalEvents.Add(new OperationalEventEntity
        {
            Category = category,
            EventType = eventType,
            PlatformEnvironment = configuration.PlatformEnvironment.ToString(),
            BrokerEnvironment = configuration.BrokerEnvironment.ToString(),
            Severity = severity,
            Summary = summary,
            DetailsJson = OperationalDataRedactor.Serialize(details),
            CorrelationId = correlationId,
            RetryCycleId = retryCycleId,
            OccurredAtUtc = timeProvider.GetUtcNow()
        });

        logger.LogInformation(
            "Operational event recorded: {Category}/{EventType} - {Summary}",
            category,
            eventType,
            summary);

        return Task.CompletedTask;
    }

    private TimeSpan GetSessionLifetime()
    {
        var configuredValue = configuration["Bootstrap:AuthSimulation:SessionLifetimeSeconds"];
        return int.TryParse(configuredValue, out var sessionLifetimeSeconds) && sessionLifetimeSeconds > 0
            ? TimeSpan.FromSeconds(sessionLifetimeSeconds)
            : TimeSpan.FromMinutes(15);
    }

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");

    private static int GetDelayBeforeAttempt(RetryPolicyConfiguration retryPolicy, int attemptNumber)
    {
        if (attemptNumber <= 1)
        {
            return retryPolicy.InitialDelaySeconds;
        }

        var delay = retryPolicy.InitialDelaySeconds;
        for (var index = 1; index < attemptNumber; index++)
        {
            delay = Math.Min(delay * retryPolicy.Multiplier, retryPolicy.MaxDelaySeconds);
        }

        return delay;
    }
}

internal sealed class PlatformAuthSupervisor(IServiceScopeFactory serviceScopeFactory, ILogger<PlatformAuthSupervisor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<PlatformStateCoordinator>();
                await coordinator.TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Platform auth supervision tick failed: {ErrorMessage}",
                    OperationalDataRedactor.RedactText(exception.Message) ?? "Unhandled failure.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
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
                await processor.ApplyAsync(stoppingToken);
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

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
