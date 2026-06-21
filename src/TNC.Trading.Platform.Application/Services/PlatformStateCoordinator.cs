using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Infrastructure.Ig;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformStateCoordinator(
    IConfiguration configuration,
    PlatformConfigurationService platformConfigurationService,
    IPlatformRuntimeStateStore runtimeStateStore,
    IPlatformIgLoginSnapshotStore igLoginSnapshotStore,
    IPlatformRetryCycleStore retryCycleStore,
    IPlatformEventStore eventStore,
    INotificationDispatcher notificationDispatcher,
    TradingScheduleGate tradingScheduleGate,
    IIgSessionClient igSessionClient,
    IProtectedCredentialService protectedCredentialService,
    IPlatformIgProofDataStore igProofDataStore,
    TimeProvider timeProvider,
    ILogger<PlatformStateCoordinator> logger)
{
    private const string MissingCredentialsBlockedReason = "IG demo credentials are incomplete.";
    private static readonly ConcurrentDictionary<Guid, byte> DegradedFailureNotificationsObservedThisProcess = new();

    public async Task<PlatformStatusModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        await TickAsync(cancellationToken).ConfigureAwait(false);

        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var currentConfiguration = await GetRuntimeConfigurationAsync(currentState, cancellationToken).ConfigureAwait(false);
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, timeProvider.GetUtcNow());
        ApplyRuntimeContext(currentConfiguration, currentState, scheduleStatus);
        var retryState = new PlatformRetryState(
            currentState.RetryPhase,
            currentState.AutomaticAttemptNumber,
            currentState.NextRetryAtUtc,
            currentState.RetryLimitReached,
            currentState.RetryLimitReached && scheduleStatus.IsActive && currentState.SessionStatus == PlatformSessionStatus.Degraded);
        var latestSnapshot = await igLoginSnapshotStore
            .GetLatestSnapshotAsync(currentConfiguration.BrokerEnvironment, cancellationToken)
            .ConfigureAwait(false);

        var latestProofData = await igProofDataStore
            .GetLatestAsync(currentConfiguration.BrokerEnvironment, cancellationToken)
            .ConfigureAwait(false);

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
            retryState,
            currentState.LastTransitionAtUtc ?? currentConfiguration.UpdatedAtUtc,
            new IgLoginStatusProjection(
                currentState.SessionStatus.ToString(),
                scheduleStatus,
                retryState,
                currentState.LastLoginAttemptAtUtc,
                currentState.LastSuccessfulLoginAtUtc,
                currentState.LatestIgLoginSnapshotId,
                currentState.LatestFailureSummary,
                latestSnapshot,
                latestProofData));
    }

    public async Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken)
    {
        await TickAsync(cancellationToken).ConfigureAwait(false);
        return await eventStore.GetEventsAsync(category, environment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ManualRetryResult> TriggerManualRetryAsync(CancellationToken cancellationToken)
    {
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var currentConfiguration = await GetRuntimeConfigurationAsync(currentState, cancellationToken).ConfigureAwait(false);
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, timeProvider.GetUtcNow());
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
        currentState.BlockedReason = MissingCredentialsBlockedReason;
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
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var currentConfiguration = await GetRuntimeConfigurationAsync(currentState, cancellationToken).ConfigureAwait(false);
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
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (currentState.SessionStatus == PlatformSessionStatus.Degraded
            && currentState.RetryPhase != AuthRetryPhase.None
            && currentState.NextRetryAtUtc is not null
            && currentState.NextRetryAtUtc > now)
        {
            currentState.LastValidatedAtUtc = now;
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (currentConfiguration.Credentials.IsComplete)
        {
            await TransitionToActiveAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
            await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        await TransitionToDegradedAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
        await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
    }

    private async Task AttemptImmediateRecoveryAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, string cycleType, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId;

        if (currentConfiguration.Credentials.IsComplete)
        {
            currentState.LastLoginAttemptAtUtc = now;
            var authAttemptCorrelationId = CreateCorrelationId();
            await RecordAuthAttemptAsync(currentConfiguration, retryCycleId, authAttemptCorrelationId, cancellationToken).ConfigureAwait(false);

            IgLoginSnapshot successfulSnapshot;

            try
            {
                successfulSnapshot = await CaptureSuccessfulLoginSnapshotAsync(currentConfiguration, now, currentState.IsDegraded, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ShouldHandleAuthFailure(ex, cancellationToken))
            {
                await TransitionToDegradedAsync(currentConfiguration, currentState, ClassifyAuthFailure(ex), cancellationToken).ConfigureAwait(false);
                return;
            }

            currentState.SessionStatus = PlatformSessionStatus.Active;
            currentState.IsDegraded = false;
            currentState.BlockedReason = null;
            currentState.LatestFailureSummary = null;
            currentState.RetryPhase = AuthRetryPhase.None;
            currentState.AutomaticAttemptNumber = 0;
            currentState.NextRetryAtUtc = null;
            currentState.RetryLimitReached = false;
            currentState.EstablishedAtUtc = now;
            currentState.ExpiresAtUtc = now.Add(GetSessionLifetime());
            currentState.LastValidatedAtUtc = now;
            currentState.LastTransitionAtUtc = now;
            currentState.LastSuccessfulLoginAtUtc = successfulSnapshot.CapturedAtUtc;
            currentState.LatestIgLoginSnapshotId = successfulSnapshot.Id;

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
        currentState.BlockedReason = MissingCredentialsBlockedReason;
        currentState.LatestFailureSummary = MissingCredentialsBlockedReason;
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
        currentState.LatestFailureSummary = null;
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

        ForgetDegradedFailureNotification(retryCycleId);
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
        currentState.LatestFailureSummary = blockedReason;
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
        ForgetDegradedFailureNotification(retryCycleId);
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
        var now = timeProvider.GetUtcNow();
        currentState.LastLoginAttemptAtUtc = now;
        var authAttemptCorrelationId = CreateCorrelationId();
        await RecordAuthAttemptAsync(currentConfiguration, retryCycleId, authAttemptCorrelationId, cancellationToken).ConfigureAwait(false);
        IgAuthenticateResponse authResponse;

        try
        {
            authResponse = await AuthenticateAsync(currentConfiguration, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ShouldHandleAuthFailure(ex, cancellationToken))
        {
            await TransitionToDegradedAsync(currentConfiguration, currentState, ClassifyAuthFailure(ex), cancellationToken).ConfigureAwait(false);
            return;
        }

        var sanitizedAuthResponse = IgAuthenticationResponseSanitizer.Sanitize(authResponse);
        var successfulSnapshot = await CaptureSuccessfulLoginSnapshotAsync(currentConfiguration, now, authResponse, cancellationToken).ConfigureAwait(false);
        await TryCaptureLiveProofDataAsync(currentConfiguration, authResponse, cancellationToken).ConfigureAwait(false);

        currentState.SessionStatus = PlatformSessionStatus.Active;
        currentState.IsDegraded = false;
        currentState.BlockedReason = null;
        currentState.LatestFailureSummary = null;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.LastSuccessfulLoginAtUtc = successfulSnapshot.CapturedAtUtc;
        currentState.LatestIgLoginSnapshotId = successfulSnapshot.Id;
        currentState.EstablishedAtUtc = now;
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

        ForgetDegradedFailureNotification(retryCycleId);
        currentState.CurrentRetryCycleId = null;

        if (wasDegraded)
        {
            await notificationDispatcher.DispatchRecoveryAsync(currentConfiguration, summary, correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task RecordAuthAttemptAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        Guid? retryCycleId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "AuthAttempted",
            $"IG {currentConfiguration.BrokerEnvironment.ToString().ToLowerInvariant()} auth attempt started.",
            new
            {
                AttemptedBrokerEnvironment = currentConfiguration.BrokerEnvironment.ToString(),
                retryCycleId
            },
            "Information",
            correlationId,
            retryCycleId,
            cancellationToken);
    }

    private async Task TransitionToDegradedAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, CancellationToken cancellationToken)
    {
        if (currentState.SessionStatus == PlatformSessionStatus.Degraded
            && string.Equals(currentState.BlockedReason, MissingCredentialsBlockedReason, StringComparison.Ordinal)
            && currentState.RetryPhase == AuthRetryPhase.None
            && currentState.AutomaticAttemptNumber == 0
            && currentState.NextRetryAtUtc is null
            && !currentState.RetryLimitReached)
        {
            if (ShouldDispatchDegradedFailureNotification(currentState.CurrentRetryCycleId))
            {
                var replayCorrelationId = CreateCorrelationId();
                await WriteOperationalEventAsync(
                    currentConfiguration,
                    "auth",
                    "FailureDetected",
                    "IG demo auth is degraded because required credentials are incomplete.",
                    new
                    {
                        RetryCycleId = currentState.CurrentRetryCycleId,
                        ReplayedAtStartup = true
                    },
                    "Warning",
                    replayCorrelationId,
                    currentState.CurrentRetryCycleId,
                    cancellationToken).ConfigureAwait(false);
                await notificationDispatcher.DispatchFailureAsync(currentConfiguration, "IG demo auth is degraded because required credentials are incomplete.", replayCorrelationId, currentState.CurrentRetryCycleId, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId ?? Guid.NewGuid();
        _ = DegradedFailureNotificationsObservedThisProcess.TryAdd(retryCycleId, 0);
        currentState.SessionStatus = PlatformSessionStatus.Degraded;
        currentState.IsDegraded = true;
        currentState.BlockedReason = MissingCredentialsBlockedReason;
        currentState.LatestFailureSummary = MissingCredentialsBlockedReason;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = retryCycleId;
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = now;
        currentState.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            "IG demo auth is degraded because required credentials are incomplete.",
            new { RetryCycleId = retryCycleId },
            "Warning",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        await notificationDispatcher.DispatchFailureAsync(currentConfiguration, "IG demo auth is degraded because required credentials are incomplete.", correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TransitionToDegradedAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        PlatformRuntimeState currentState,
        string failureSummary,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId ?? Guid.NewGuid();
        var nextDelay = GetDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);

        _ = DegradedFailureNotificationsObservedThisProcess.TryAdd(retryCycleId, 0);
        currentState.SessionStatus = PlatformSessionStatus.Degraded;
        currentState.IsDegraded = true;
        currentState.BlockedReason = failureSummary;
        currentState.LatestFailureSummary = failureSummary;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = retryCycleId;
        currentState.EstablishedAtUtc = null;
        currentState.ExpiresAtUtc = null;
        currentState.LastValidatedAtUtc = now;
        currentState.LastTransitionAtUtc = now;

        await UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            failureSummary,
            new { RetryCycleId = retryCycleId },
            "Warning",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        await notificationDispatcher.DispatchFailureAsync(currentConfiguration, failureSummary, correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
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
        currentState.LatestFailureSummary = currentState.BlockedReason;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = Guid.NewGuid();
        currentState.LastLoginAttemptAtUtc = now;
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

    private async Task<IgLoginSnapshot> CaptureSuccessfulLoginSnapshotAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        DateTimeOffset capturedAtUtc,
        bool wasDegraded,
        CancellationToken cancellationToken)
    {
        _ = wasDegraded;

        var authResponse = await AuthenticateAsync(currentConfiguration, cancellationToken).ConfigureAwait(false);
        return await CaptureSuccessfulLoginSnapshotAsync(currentConfiguration, capturedAtUtc, authResponse, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IgLoginSnapshot> CaptureSuccessfulLoginSnapshotAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        DateTimeOffset capturedAtUtc,
        IgAuthenticateResponse response,
        CancellationToken cancellationToken)
    {
        var snapshot = IgLoginSnapshotMapper.MapLatestSnapshot(currentConfiguration.BrokerEnvironment, response, capturedAtUtc);
        await igLoginSnapshotStore.CaptureSuccessfulSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);

        await WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "SnapshotCaptured",
            $"IG login snapshot captured for trading day {snapshot.TradingDay:d}.",
            new
            {
                snapshot.Id,
                snapshot.TradingDay,
                snapshot.CurrentAccountId
            },
            "Information",
            CreateCorrelationId(),
            null,
            cancellationToken).ConfigureAwait(false);

        return snapshot;
    }

    private async Task<IgAuthenticateResponse> AuthenticateAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        CancellationToken cancellationToken)
    {
        var credentials = await protectedCredentialService
            .GetCredentialsAsync(currentConfiguration.BrokerEnvironment, cancellationToken)
            .ConfigureAwait(false);
        var authRequest = new IgAuthenticateRequest(
            currentConfiguration.BrokerEnvironment,
            credentials.ApiKey,
            credentials.Identifier,
            credentials.Password);

        return await igSessionClient.AuthenticateAsync(authRequest, cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldHandleAuthFailure(Exception exception, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or TaskCanceledException or OperationCanceledException { InnerException: TimeoutException };
    }

    private static string ClassifyAuthFailure(Exception ex)
    {
        return ex switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } =>
                "IG authentication failed: invalid or rejected credentials.",
            HttpRequestException { StatusCode: HttpStatusCode.Forbidden } =>
                "IG authentication failed: access forbidden.",
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
                "IG authentication failed: request rate limit exceeded.",
            HttpRequestException { StatusCode: not null } =>
                "IG authentication failed: unexpected broker response.",
            TaskCanceledException or OperationCanceledException =>
                "IG authentication failed: request timed out.",
            HttpRequestException =>
                "IG authentication failed: broker is unreachable.",
            _ =>
                "IG authentication failed: unexpected error."
        };
    }

    internal Task UpsertRetryCycleAsync(
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

    private async Task TryCaptureLiveProofDataAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        IgAuthenticateResponse authResponse,
        CancellationToken cancellationToken)
    {
        // Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7
        var cst = authResponse.ClientSessionToken;
        var securityToken = authResponse.AccountSecurityToken;
        var apiKey = authResponse.Headers.GetValueOrDefault("X-IG-API-KEY") ?? string.Empty;

        if (string.IsNullOrEmpty(cst) || string.IsNullOrEmpty(securityToken))
        {
            logger.LogWarning("Proof-data query skipped: session tokens not present in auth response.");
            return;
        }

        try
        {
            var accountsResponse = await igSessionClient
                .GetAccountsAsync(cst, securityToken, apiKey, cancellationToken)
                .ConfigureAwait(false);

            var positionsResponse = await igSessionClient
                .GetPositionsAsync(cst, securityToken, apiKey, cancellationToken)
                .ConfigureAwait(false);

            var preferred = accountsResponse.Accounts.FirstOrDefault(a => a.Preferred)
                         ?? accountsResponse.Accounts.FirstOrDefault();

            var snapshot = new IgProofDataSnapshot(
                preferred?.AccountName,
                preferred?.AccountId,
                preferred?.Balance?.Balance,
                positionsResponse.Positions.Count,
                timeProvider.GetUtcNow());

            await igProofDataStore
                .SaveAsync(currentConfiguration.BrokerEnvironment, snapshot, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "IG proof data captured: account={AccountName}, balance={Balance}, positions={PositionCount}",
                preferred?.AccountName ?? "none",
                preferred?.Balance?.Balance,
                positionsResponse.Positions.Count);
        }
        catch (Exception ex)
        {
            // Proof-data failure is non-fatal: log and continue; session is already active.
            logger.LogWarning(ex, "IG proof-data query failed; session remains active.");
        }
    }

    private TimeSpan GetSessionLifetime()
    {
        var configuredValue = configuration["Bootstrap:AuthSimulation:SessionLifetimeSeconds"];
        return int.TryParse(configuredValue, out var sessionLifetimeSeconds) && sessionLifetimeSeconds > 0
            ? TimeSpan.FromSeconds(sessionLifetimeSeconds)
            : TimeSpan.FromMinutes(15);
    }

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");

    private static bool ShouldDispatchDegradedFailureNotification(Guid? retryCycleId)
    {
        return retryCycleId is Guid value
            && DegradedFailureNotificationsObservedThisProcess.TryAdd(value, 0);
    }

    private static void ForgetDegradedFailureNotification(Guid? retryCycleId)
    {
        if (retryCycleId is Guid value)
        {
            _ = DegradedFailureNotificationsObservedThisProcess.TryRemove(value, out _);
        }
    }

    private async Task<PlatformConfigurationSnapshot> GetRuntimeConfigurationAsync(
        PlatformRuntimeState currentState,
        CancellationToken cancellationToken)
    {
        var platformEnvironment = TryParsePlatformEnvironment(currentState.PlatformEnvironment);
        var brokerEnvironment = TryParseBrokerEnvironment(currentState.BrokerEnvironment);

        return await platformConfigurationService
            .GetRuntimeAsync(platformEnvironment, brokerEnvironment, cancellationToken)
            .ConfigureAwait(false);
    }

    private static PlatformEnvironmentKind? TryParsePlatformEnvironment(string? value)
    {
        return Enum.TryParse<PlatformEnvironmentKind>(value, ignoreCase: true, out var platformEnvironment)
            ? platformEnvironment
            : null;
    }

    private static BrokerEnvironmentKind? TryParseBrokerEnvironment(string? value)
    {
        return Enum.TryParse<BrokerEnvironmentKind>(value, ignoreCase: true, out var brokerEnvironment)
            ? brokerEnvironment
            : null;
    }

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
