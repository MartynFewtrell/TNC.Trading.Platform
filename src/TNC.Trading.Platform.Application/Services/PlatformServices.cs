using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Application.Features.GetPlatformEvents;
using TNC.Trading.Platform.Application.Features.GetPlatformStatus;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Infrastructure.Ig;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformConfigurationStore
{
    Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken);

    Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken);
}

internal interface IPlatformRuntimeStateStore
{
    Task<PlatformRuntimeState> GetOrCreateAsync(CancellationToken cancellationToken);

    Task SaveAsync(PlatformRuntimeState state, CancellationToken cancellationToken);
}

internal interface IPlatformRetryCycleStore
{
    Task UpsertAsync(PlatformRetryCycle cycle, CancellationToken cancellationToken);
}

internal interface IPlatformEventStore
{
    Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken);

    Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken);
}

internal interface INotificationDispatcher
{
    Task DispatchFailureAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);

    Task DispatchRetryLimitReachedAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);

    Task DispatchRecoveryAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);

    Task DispatchBlockedLiveAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);
}

internal static class PlatformApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        services.AddScoped<PlatformConfigurationService>();
        services.AddScoped<TradingScheduleGate>();
        services.AddScoped<PlatformStateCoordinator>();
        services.AddScoped<GetPlatformStatusHandler>();
        services.AddScoped<GetPlatformConfigurationHandler>();
        services.AddScoped<UpdatePlatformConfigurationHandler>();
        services.AddScoped<TriggerManualAuthRetryHandler>();
        services.AddScoped<GetPlatformEventsHandler>();
        services.AddHostedService<PlatformAuthSupervisor>();

        return services;
    }
}

internal sealed class PlatformConfigurationService(IPlatformConfigurationStore store)
{
    public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
        store.GetCurrentAsync(cancellationToken);

    public Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken) =>
        store.UpdateAsync(update, cancellationToken);
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

internal sealed class PlatformStateCoordinator(
    IConfiguration configuration,
    PlatformConfigurationService platformConfigurationService,
    IPlatformRuntimeStateStore runtimeStateStore,
    IPlatformRetryCycleStore retryCycleStore,
    IPlatformEventStore eventStore,
    INotificationDispatcher notificationDispatcher,
    TradingScheduleGate tradingScheduleGate,
    TimeProvider timeProvider,
    ILogger<PlatformStateCoordinator> logger)
{
    public async Task<PlatformStatusModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        await TickAsync(cancellationToken).ConfigureAwait(false);

        var currentConfiguration = await platformConfigurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, timeProvider.GetUtcNow());
        ApplyRuntimeContext(currentConfiguration, currentState, scheduleStatus);

        return new PlatformStatusModel(
            currentConfiguration.PlatformEnvironment,
            currentConfiguration.BrokerEnvironment,
            currentConfiguration.LiveOptionVisible,
            currentConfiguration.LiveOptionAvailable,
            currentConfiguration.TradingSchedule,
            scheduleStatus,
            currentState.SessionStatus,
            currentState.IsDegraded,
            currentState.BlockedReason,
            new PlatformRetryState(
                currentState.RetryPhase,
                currentState.AutomaticAttemptNumber,
                currentState.NextRetryAtUtc,
                currentState.RetryLimitReached,
                currentState.RetryLimitReached && scheduleStatus.IsActive && currentState.SessionStatus == PlatformSessionStatus.Degraded),
            currentState.LastTransitionAtUtc ?? currentConfiguration.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken)
    {
        await TickAsync(cancellationToken).ConfigureAwait(false);
        return await eventStore.GetEventsAsync(category, environment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ManualRetryResult> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        var currentConfiguration = await platformConfigurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, timeProvider.GetUtcNow());
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        ApplyRuntimeContext(currentConfiguration, currentState, scheduleStatus);

        if (!scheduleStatus.IsActive)
        {
            throw new InvalidOperationException("Manual retry is unavailable while the trading schedule is inactive.");
        }

        if (currentConfiguration.PlatformEnvironment == PlatformEnvironmentKind.Test && currentConfiguration.BrokerEnvironment == BrokerEnvironmentKind.Live)
        {
            await HandleBlockedLiveAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("IG live is unavailable while the platform environment is Test.");
        }

        if (!currentState.RetryLimitReached || currentState.SessionStatus != PlatformSessionStatus.Degraded)
        {
            throw new InvalidOperationException("Manual retry becomes available only after the initial automatic retries are exhausted.");
        }

        var now = timeProvider.GetUtcNow();
        var cycleId = Guid.NewGuid();
        var nextDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);

        currentState.CurrentRetryCycleId = cycleId;
        currentState.AutomaticAttemptNumber = 0;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.RetryLimitReached = false;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.SessionStatus = PlatformSessionStatus.Degraded;
        currentState.IsDegraded = true;
        currentState.BlockedReason = "IG demo credentials are incomplete.";
        currentState.EstablishedAtUtc = null;
        currentState.LastValidatedAtUtc = now;
        currentState.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(cycleId, currentConfiguration, currentState, "Manual", failureNotificationSent: false, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "ManualRetryRequested",
            "Manual retry requested for the current degraded auth cycle.",
            new { RetryCycleId = cycleId },
            "Information",
            correlationId,
            cycleId,
            cancellationToken).ConfigureAwait(false);

        await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);

        await AttemptImmediateRecoveryAsync(currentConfiguration, currentState, "Manual", cancellationToken).ConfigureAwait(false);
        await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);

        return new ManualRetryResult(cycleId);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var currentConfiguration = await platformConfigurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, now);
        ApplyRuntimeContext(currentConfiguration, currentState, scheduleStatus);

        if (!scheduleStatus.IsActive)
        {
            await TransitionToOutOfScheduleAsync(currentConfiguration, currentState, scheduleStatus.Reason, cancellationToken).ConfigureAwait(false);
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (currentConfiguration.PlatformEnvironment == PlatformEnvironmentKind.Test && currentConfiguration.BrokerEnvironment == BrokerEnvironmentKind.Live)
        {
            await HandleBlockedLiveAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (HasSessionExpired(currentState, now))
        {
            await HandleSessionExpiredAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
        }

        if (currentConfiguration.Credentials.IsComplete)
        {
            await TransitionToActiveAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        await TransitionToDegradedAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);

        if (currentState.NextRetryAtUtc is not null && currentState.NextRetryAtUtc <= now)
        {
            if (currentState.RetryPhase == AuthRetryPhase.InitialAutomatic)
            {
                currentState.AutomaticAttemptNumber += 1;
                if (currentState.AutomaticAttemptNumber >= currentConfiguration.RetryPolicy.MaxAutomaticRetries)
                {
                    currentState.RetryPhase = AuthRetryPhase.Periodic;
                    currentState.RetryLimitReached = true;
                    currentState.NextRetryAtUtc = now.AddMinutes(currentConfiguration.RetryPolicy.PeriodicDelayMinutes);
                    currentState.LastTransitionAtUtc = now;

                    var lastDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, currentState.AutomaticAttemptNumber);
                    var summary = $"Initial automatic IG demo auth retries are exhausted after {currentState.AutomaticAttemptNumber} attempts. Periodic retry continues every {currentConfiguration.RetryPolicy.PeriodicDelayMinutes} minutes.";
                    var correlationId = CreateCorrelationId();

                    await UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelay, cancellationToken).ConfigureAwait(false);
                    await WriteOperationalEventAsync(
                        currentConfiguration,
                        "auth",
                        "RetryLimitReached",
                        summary,
                        new
                        {
                            currentState.AutomaticAttemptNumber,
                            LastScheduledDelaySeconds = lastDelay,
                            currentConfiguration.RetryPolicy.PeriodicDelayMinutes
                        },
                        "Warning",
                        correlationId,
                        currentState.CurrentRetryCycleId,
                        cancellationToken).ConfigureAwait(false);
                    await notificationDispatcher.DispatchRetryLimitReachedAsync(currentConfiguration, summary, correlationId, currentState.CurrentRetryCycleId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var nextDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, currentState.AutomaticAttemptNumber + 1);
                    currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
                    currentState.LastTransitionAtUtc = now;

                    await UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);
                    await WriteOperationalEventAsync(
                        currentConfiguration,
                        "auth",
                        "RetryScheduled",
                        $"Automatic retry {currentState.AutomaticAttemptNumber} failed. Next retry is scheduled at {currentState.NextRetryAtUtc:O}.",
                        new
                        {
                            currentState.AutomaticAttemptNumber,
                            currentState.NextRetryAtUtc
                        },
                        "Information",
                        CreateCorrelationId(),
                        currentState.CurrentRetryCycleId,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                currentState.NextRetryAtUtc = now.AddMinutes(currentConfiguration.RetryPolicy.PeriodicDelayMinutes);
                currentState.LastTransitionAtUtc = now;

                await UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, currentConfiguration.RetryPolicy.PeriodicDelayMinutes * 60, cancellationToken).ConfigureAwait(false);
                await WriteOperationalEventAsync(
                    currentConfiguration,
                    "auth",
                    "PeriodicRetryScheduled",
                    $"Periodic retry remains active. Next retry is scheduled at {currentState.NextRetryAtUtc:O}.",
                    new { currentState.NextRetryAtUtc },
                    "Information",
                    CreateCorrelationId(),
                    currentState.CurrentRetryCycleId,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
    }

    private async Task AttemptImmediateRecoveryAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, string cycleType, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId;

        if (currentConfiguration.Credentials.IsComplete)
        {
            currentState.SessionStatus = PlatformSessionStatus.Active;
            currentState.IsDegraded = false;
            currentState.BlockedReason = null;
            currentState.RetryPhase = AuthRetryPhase.None;
            currentState.AutomaticAttemptNumber = 0;
            currentState.NextRetryAtUtc = null;
            currentState.RetryLimitReached = false;
            currentState.EstablishedAtUtc = now;
            currentState.ExpiresAtUtc = now.Add(GetSessionLifetime());
            currentState.LastValidatedAtUtc = now;
            currentState.LastTransitionAtUtc = now;

            await UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, cycleType, failureNotificationSent: false, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

            var recoveryCorrelationId = CreateCorrelationId();
            await WriteOperationalEventAsync(
                currentConfiguration,
                "auth",
                "Recovered",
                "IG demo auth recovered after manual retry.",
                new { RetryCycleId = retryCycleId },
                "Information",
                recoveryCorrelationId,
                retryCycleId,
                cancellationToken).ConfigureAwait(false);
            currentState.CurrentRetryCycleId = null;
            await notificationDispatcher.DispatchRecoveryAsync(currentConfiguration, "IG demo auth recovered after manual retry.", recoveryCorrelationId, retryCycleId, cancellationToken).ConfigureAwait(false);
            return;
        }

        currentState.SessionStatus = PlatformSessionStatus.Degraded;
        currentState.IsDegraded = true;
        currentState.BlockedReason = "IG demo credentials are incomplete.";
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        var nextDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = now;
        currentState.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, cycleType, failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var failureCorrelationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            "Manual retry started a new degraded auth cycle because required IG demo credentials are still missing.",
            new { RetryCycleId = retryCycleId },
            "Warning",
            failureCorrelationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        await notificationDispatcher.DispatchFailureAsync(currentConfiguration, "Manual retry started a new degraded auth cycle because required IG demo credentials are still missing.", failureCorrelationId, retryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TransitionToOutOfScheduleAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, string reason, CancellationToken cancellationToken)
    {
        if (currentState.SessionStatus == PlatformSessionStatus.OutOfSchedule
            && string.Equals(currentState.BlockedReason, reason, StringComparison.Ordinal))
        {
            return;
        }

        var retryCycleId = currentState.CurrentRetryCycleId;
        currentState.SessionStatus = PlatformSessionStatus.OutOfSchedule;
        currentState.IsDegraded = false;
        currentState.BlockedReason = reason;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = timeProvider.GetUtcNow();
        currentState.LastTransitionAtUtc = timeProvider.GetUtcNow();

        await UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "TradingScheduleInactive",
            reason,
            new { TradingScheduleActive = false },
            "Information",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);

        currentState.CurrentRetryCycleId = null;
    }

    private async Task HandleBlockedLiveAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, CancellationToken cancellationToken)
    {
        const string blockedReason = "IG live is unavailable while the platform environment is Test.";
        if (currentState.SessionStatus == PlatformSessionStatus.Blocked
            && string.Equals(currentState.BlockedReason, blockedReason, StringComparison.Ordinal))
        {
            return;
        }

        var retryCycleId = currentState.CurrentRetryCycleId;
        currentState.SessionStatus = PlatformSessionStatus.Blocked;
        currentState.IsDegraded = true;
        currentState.BlockedReason = blockedReason;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = timeProvider.GetUtcNow();
        currentState.LastTransitionAtUtc = timeProvider.GetUtcNow();

        await UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "BlockedLiveAttempt",
            "A live broker action was blocked because the platform environment is Test.",
            new { currentConfiguration.PlatformEnvironment, currentConfiguration.BrokerEnvironment },
            "Warning",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        currentState.CurrentRetryCycleId = null;
        await notificationDispatcher.DispatchBlockedLiveAsync(currentConfiguration, "A live broker action was blocked because the platform environment is Test.", correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TransitionToActiveAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, CancellationToken cancellationToken)
    {
        if (currentState.SessionStatus == PlatformSessionStatus.Active)
        {
            currentState.LastValidatedAtUtc = timeProvider.GetUtcNow();
            currentState.ExpiresAtUtc ??= currentState.LastValidatedAtUtc.Value.Add(GetSessionLifetime());
            return;
        }

        var wasDegraded = currentState.IsDegraded;
        var retryCycleId = currentState.CurrentRetryCycleId;
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

        currentState.SessionStatus = PlatformSessionStatus.Active;
        currentState.IsDegraded = false;
        currentState.BlockedReason = null;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.EstablishedAtUtc = timeProvider.GetUtcNow();
        currentState.ExpiresAtUtc = currentState.EstablishedAtUtc.Value.Add(GetSessionLifetime());
        currentState.LastValidatedAtUtc = currentState.EstablishedAtUtc;
        currentState.LastTransitionAtUtc = currentState.EstablishedAtUtc;

        await UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: wasDegraded, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var eventType = wasDegraded ? "Recovered" : "Authenticated";
        var summary = wasDegraded
            ? "IG demo auth is healthy again."
            : "IG demo auth is active for the configured trading schedule.";

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            eventType,
            summary,
            new
            {
                SessionStatus = currentState.SessionStatus.ToString(),
                SanitizedAuthResponse = sanitizedAuthResponse
            },
            "Information",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);

        currentState.CurrentRetryCycleId = null;

        if (wasDegraded)
        {
            await notificationDispatcher.DispatchRecoveryAsync(currentConfiguration, summary, correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TransitionToDegradedAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, CancellationToken cancellationToken)
    {
        if (currentState.SessionStatus == PlatformSessionStatus.Degraded)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        currentState.SessionStatus = PlatformSessionStatus.Degraded;
        currentState.IsDegraded = true;
        currentState.BlockedReason = "IG demo credentials are incomplete.";
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        var nextDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = Guid.NewGuid();
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = now;
        currentState.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            "IG demo auth is degraded because required credentials are incomplete.",
            new { RetryCycleId = currentState.CurrentRetryCycleId },
            "Warning",
            correlationId,
            currentState.CurrentRetryCycleId,
            cancellationToken).ConfigureAwait(false);
        await notificationDispatcher.DispatchFailureAsync(currentConfiguration, "IG demo auth is degraded because required credentials are incomplete.", correlationId, currentState.CurrentRetryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyRuntimeContext(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, TradingScheduleStatus scheduleStatus)
    {
        currentState.PlatformEnvironment = currentConfiguration.PlatformEnvironment.ToString();
        currentState.BrokerEnvironment = currentConfiguration.BrokerEnvironment.ToString();
        currentState.TradingScheduleStatus = scheduleStatus.IsActive ? "Active" : "Inactive";
    }

    private async Task HandleSessionExpiredAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var nextDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);
        var expiredAtUtc = currentState.ExpiresAtUtc;

        currentState.SessionStatus = PlatformSessionStatus.Degraded;
        currentState.IsDegraded = true;
        currentState.BlockedReason = "The active IG demo session expired and is being re-established.";
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = Guid.NewGuid();
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = now;
        currentState.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        var summary = "The active IG demo session expired and re-authentication started.";

        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "SessionExpired",
            summary,
            new
            {
                RetryCycleId = currentState.CurrentRetryCycleId,
                expiredAtUtc
            },
            "Warning",
            correlationId,
            currentState.CurrentRetryCycleId,
            cancellationToken).ConfigureAwait(false);

        await notificationDispatcher.DispatchFailureAsync(currentConfiguration, summary, correlationId, currentState.CurrentRetryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasSessionExpired(PlatformRuntimeState currentState, DateTimeOffset now)
    {
        return currentState.SessionStatus == PlatformSessionStatus.Active
            && currentState.ExpiresAtUtc is not null
            && currentState.ExpiresAtUtc <= now;
    }

    private Task UpsertRetryCycleAsync(
        Guid? retryCycleId,
        PlatformConfigurationSnapshot currentConfiguration,
        PlatformRuntimeState currentState,
        string cycleType,
        bool failureNotificationSent,
        int? lastDelaySeconds,
        CancellationToken cancellationToken)
    {
        if (retryCycleId is null)
        {
            return Task.CompletedTask;
        }

        return retryCycleStore.UpsertAsync(
            new PlatformRetryCycle
            {
                RetryCycleId = retryCycleId.Value,
                CycleType = cycleType,
                PlatformEnvironment = currentConfiguration.PlatformEnvironment.ToString(),
                BrokerEnvironment = currentConfiguration.BrokerEnvironment.ToString(),
                RetryPhase = currentState.RetryPhase,
                AutomaticAttemptNumber = currentState.AutomaticAttemptNumber,
                NextRetryAtUtc = currentState.NextRetryAtUtc,
                LastDelaySeconds = lastDelaySeconds,
                PeriodicDelayMinutes = currentConfiguration.RetryPolicy.PeriodicDelayMinutes,
                MaxAutomaticRetries = currentConfiguration.RetryPolicy.MaxAutomaticRetries,
                RetryLimitReached = currentState.RetryLimitReached,
                FailureNotificationSent = failureNotificationSent,
                StartedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            },
            cancellationToken);
    }

    private async Task WriteOperationalEventAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        string category,
        string eventType,
        string summary,
        object details,
        string severity,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        await eventStore.AddAsync(
            new PlatformEventRecord(
                category,
                eventType,
                currentConfiguration.PlatformEnvironment,
                currentConfiguration.BrokerEnvironment,
                severity,
                summary,
                details,
                correlationId,
                retryCycleId,
                timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Operational event recorded: {Category}/{EventType} - {Summary}",
            category,
            eventType,
            summary);
    }

    private TimeSpan GetSessionLifetime()
    {
        var configuredValue = configuration["Bootstrap:AuthSimulation:SessionLifetimeSeconds"];
        return int.TryParse(configuredValue, out var sessionLifetimeSeconds) && sessionLifetimeSeconds > 0
            ? TimeSpan.FromSeconds(sessionLifetimeSeconds)
            : TimeSpan.FromMinutes(15);
    }

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");

    internal static int GetDelayBeforeAttempt(RetryPolicyConfiguration retryPolicy, int attemptNumber)
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
                await coordinator.TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Platform auth supervision tick failed: {ErrorMessage}",
                    exception.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
