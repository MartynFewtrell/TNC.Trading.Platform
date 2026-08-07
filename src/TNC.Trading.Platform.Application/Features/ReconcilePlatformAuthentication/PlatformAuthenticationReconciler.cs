using System.Collections.Concurrent;
using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

internal sealed class PlatformAuthenticationReconciler(
    PlatformAuthSimulationSettings authSimulationSettings,
    PlatformConfigurationService platformConfigurationService,
    IPlatformRuntimeStateStore runtimeStateStore,
    IPlatformIgLoginSnapshotStore igLoginSnapshotStore,
    IPlatformRetryCycleStore retryCycleStore,
    IPlatformEventStore eventStore,
    INotificationDispatcher notificationDispatcher,
    TradingScheduleGate tradingScheduleGate,
    IBrokerAuthenticationGateway brokerAuthenticationGateway,
    IPlatformIgProofDataStore igProofDataStore,
    TimeProvider timeProvider,
    IPlatformApplicationLogger logger,
    IPlatformReconciliationLease reconciliationLease,
    IAccountDetailsDailyCapture? accountDetailsDailyCapture = null) : IPlatformAuthenticationReconciler
{
    private const string MissingCredentialsBlockedReason = "IG demo credentials are incomplete.";
    private const string UnusableCredentialsBlockedReason = "IG Demo credentials must be re-entered.";
    private static readonly ConcurrentDictionary<Guid, byte> DegradedFailureNotificationsObservedThisProcess = new();
    private readonly PlatformStateTransitionEngine transitionEngine = new();
    private readonly PlatformAuthenticationReconcilerSideEffects sideEffects = new(retryCycleStore, eventStore, notificationDispatcher, timeProvider, logger);

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
        await using var lease = await reconciliationLease.AcquireAsync(cancellationToken).ConfigureAwait(false);
        return await TriggerManualRetryCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlatformRuntimeState> ReconcileAsync(CancellationToken cancellationToken)
    {
        await using var lease = await reconciliationLease.AcquireAsync(cancellationToken).ConfigureAwait(false);
        return await ReconcileCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        _ = await ReconcileAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ManualRetryResult> TriggerManualRetryCoreAsync(CancellationToken cancellationToken)
    {
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var currentConfiguration = await GetRuntimeConfigurationAsync(currentState, cancellationToken).ConfigureAwait(false);
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, timeProvider.GetUtcNow());
        ApplyRuntimeContext(currentConfiguration, currentState, scheduleStatus);
        var scheduleDecision = tradingScheduleGate.DecideTickAction(
            currentConfiguration.PlatformEnvironment,
            currentConfiguration.BrokerEnvironment,
            scheduleStatus);

        if (scheduleDecision.Action == TradingScheduleTickAction.BlockedBySchedule)
        {
            throw new InvalidOperationException("Manual retry is unavailable while the trading schedule is inactive.");
        }

        if (scheduleDecision.Action == TradingScheduleTickAction.BlockedLive)
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
        var nextDelay = RetryTimingPolicy.CalculateDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);

        currentState.CurrentRetryCycleId = cycleId;
        currentState.AutomaticAttemptNumber = 0;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.RetryLimitReached = false;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.Degraded, MissingCredentialsBlockedReason, now));

        await sideEffects.UpsertRetryCycleAsync(cycleId, currentConfiguration, currentState, "Manual", failureNotificationSent: false, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
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

    private async Task<PlatformRuntimeState> ReconcileCoreAsync(CancellationToken cancellationToken)
    {
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var currentConfiguration = await GetRuntimeConfigurationAsync(currentState, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var scheduleStatus = tradingScheduleGate.Evaluate(currentConfiguration.TradingSchedule, now);
        ApplyRuntimeContext(currentConfiguration, currentState, scheduleStatus);
        var scheduleDecision = tradingScheduleGate.DecideTickAction(
            currentConfiguration.PlatformEnvironment,
            currentConfiguration.BrokerEnvironment,
            scheduleStatus);

        switch (scheduleDecision.Action)
        {
            case TradingScheduleTickAction.BlockedBySchedule:
                await TransitionToOutOfScheduleAsync(currentConfiguration, currentState, scheduleDecision.Reason!, cancellationToken).ConfigureAwait(false);
                break;
            case TradingScheduleTickAction.BlockedLive:
                await HandleBlockedLiveAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
                break;
            case TradingScheduleTickAction.Allowed:
                await ApplyStateTransitionAsync(currentConfiguration, currentState, now, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported trading schedule tick action '{scheduleDecision.Action}'.");
        }

        await runtimeStateStore.SaveAsync(currentState, cancellationToken).ConfigureAwait(false);
        return currentState;
    }

    private async Task ApplyStateTransitionAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        PlatformRuntimeState currentState,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var decision = transitionEngine.DecideTickAction(currentConfiguration, currentState, now);

        switch (decision.Kind)
        {
            case PlatformTickDecisionKind.HandleSessionExpired:
                await HandleSessionExpiredAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
                break;
            case PlatformTickDecisionKind.WaitForScheduledRetry:
                currentState.LastValidatedAtUtc = now;
                break;
            case PlatformTickDecisionKind.TransitionToActive:
                await TransitionToActiveAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
                break;
            case PlatformTickDecisionKind.TransitionToDegraded:
                await TransitionToDegradedAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported tick decision '{decision.Kind}'.");
        }
    }

    private async Task AttemptImmediateRecoveryAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, string cycleType, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId;

        if (currentConfiguration.Credentials.IsAuthenticationReady)
        {
            currentState.LastLoginAttemptAtUtc = now;
            var authAttemptCorrelationId = CreateCorrelationId();
            await RecordAuthAttemptAsync(currentConfiguration, retryCycleId, authAttemptCorrelationId, cancellationToken).ConfigureAwait(false);

            var authentication = await AuthenticateAsync(currentConfiguration, cancellationToken).ConfigureAwait(false);
            if (!authentication.IsAuthenticated)
            {
                await TransitionToDegradedAsync(currentConfiguration, currentState, authentication.Failure!, cancellationToken).ConfigureAwait(false);
                return;
            }

            var successfulSnapshot = await CaptureSuccessfulLoginSnapshotAsync(
                currentConfiguration,
                now,
                authentication.Evidence!,
                cancellationToken).ConfigureAwait(false);
            await CaptureProofDataAsync(currentConfiguration, authentication.Proof, cancellationToken).ConfigureAwait(false);

            currentState.LatestFailureSummary = null;
            currentState.RetryPhase = AuthRetryPhase.None;
            currentState.AutomaticAttemptNumber = 0;
            currentState.NextRetryAtUtc = null;
            currentState.RetryLimitReached = false;
            ApplyAuthenticationTransitionOrThrow(
                currentState,
                new AuthenticationStateTransition(
                    PlatformSessionStatus.Active,
                    null,
                    now,
                    now,
                    now.Add(GetSessionLifetime())));
            currentState.LastSuccessfulLoginAtUtc = successfulSnapshot.CapturedAtUtc;
            currentState.LatestIgLoginSnapshotId = successfulSnapshot.Id;

            await sideEffects.UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, cycleType, failureNotificationSent: false, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

            var recoveryCorrelationId = CreateCorrelationId();
            await sideEffects.WriteOperationalEventAsync(
                currentConfiguration,
                "auth",
                "Recovered",
                "IG demo auth recovered after manual retry.",
                new { RetryCycleId = retryCycleId },
                "Information",
                recoveryCorrelationId,
                retryCycleId,
                cancellationToken).ConfigureAwait(false);

            if (accountDetailsDailyCapture is not null)
            {
                try
                {
                    await accountDetailsDailyCapture.HandleAsync(new CaptureDailyAccountDetailsRequest(), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Account Details is best-effort and must not invalidate durable login success.
                }
            }

            currentState.CurrentRetryCycleId = null;
            await sideEffects.DispatchRecoveryAsync(currentConfiguration, "IG demo auth recovered after manual retry.", recoveryCorrelationId, retryCycleId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var blockedReason = currentConfiguration.Credentials.RequiresCredentialReentry
            ? UnusableCredentialsBlockedReason
            : MissingCredentialsBlockedReason;
        currentState.LatestFailureSummary = blockedReason;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        var nextDelay = RetryTimingPolicy.CalculateDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.Degraded, blockedReason, now));

        await sideEffects.UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, cycleType, failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var failureCorrelationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            "Manual retry started a new degraded auth cycle because required IG demo credentials are still missing.",
            new { RetryCycleId = retryCycleId },
            "Warning",
            failureCorrelationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        await sideEffects.DispatchFailureAsync(currentConfiguration, "Manual retry started a new degraded auth cycle because required IG demo credentials are still missing.", failureCorrelationId, retryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TransitionToOutOfScheduleAsync(PlatformConfigurationSnapshot currentConfiguration, PlatformRuntimeState currentState, string reason, CancellationToken cancellationToken)
    {
        if (currentState.SessionStatus == PlatformSessionStatus.OutOfSchedule
            && string.Equals(currentState.BlockedReason, reason, StringComparison.Ordinal))
        {
            return;
        }

        var retryCycleId = currentState.CurrentRetryCycleId;
        currentState.LatestFailureSummary = null;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.OutOfSchedule, reason, timeProvider.GetUtcNow()));

        await sideEffects.UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
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
        currentState.LatestFailureSummary = blockedReason;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.Blocked, blockedReason, timeProvider.GetUtcNow()));

        await sideEffects.UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
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
        await sideEffects.DispatchBlockedLiveAsync(currentConfiguration, "A live broker action was blocked because the platform environment is Test.", correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
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
        if (!currentConfiguration.Credentials.IsAuthenticationReady)
        {
            await TransitionToDegradedAsync(currentConfiguration, currentState, cancellationToken).ConfigureAwait(false);
            return;
        }

        var authentication = await AuthenticateAsync(currentConfiguration, cancellationToken).ConfigureAwait(false);
        if (!authentication.IsAuthenticated)
        {
            await TransitionToDegradedAsync(currentConfiguration, currentState, authentication.Failure!, cancellationToken).ConfigureAwait(false);
            return;
        }

        var successfulSnapshot = await CaptureSuccessfulLoginSnapshotAsync(
            currentConfiguration,
            now,
            authentication.Evidence!,
            cancellationToken).ConfigureAwait(false);
        await CaptureProofDataAsync(currentConfiguration, authentication.Proof, cancellationToken).ConfigureAwait(false);

        currentState.LatestFailureSummary = null;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.LastSuccessfulLoginAtUtc = successfulSnapshot.CapturedAtUtc;
        currentState.LatestIgLoginSnapshotId = successfulSnapshot.Id;
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(
                PlatformSessionStatus.Active,
                null,
                now,
                now,
                now.Add(GetSessionLifetime())));

        await sideEffects.UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: wasDegraded, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var eventType = wasDegraded ? "Recovered" : "Authenticated";
        var summary = wasDegraded
            ? "IG demo auth is healthy again."
            : "IG demo auth is active for the configured trading schedule.";

        var correlationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            eventType,
            summary,
            new
            {
                SessionStatus = currentState.SessionStatus.ToString(),
                AuthenticationEvidence = authentication.Evidence
            },
            "Information",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);

        if (accountDetailsDailyCapture is not null)
        {
            try
            {
                await accountDetailsDailyCapture.HandleAsync(new CaptureDailyAccountDetailsRequest(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Account Details is best-effort and must not invalidate durable login success.
            }
        }

        ForgetDegradedFailureNotification(retryCycleId);
        currentState.CurrentRetryCycleId = null;

        if (wasDegraded)
        {
            await sideEffects.DispatchRecoveryAsync(currentConfiguration, summary, correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task RecordAuthAttemptAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        Guid? retryCycleId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return sideEffects.WriteOperationalEventAsync(
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
        var blockedReason = currentConfiguration.Credentials.RequiresCredentialReentry
            ? UnusableCredentialsBlockedReason
            : MissingCredentialsBlockedReason;

        if (currentState.SessionStatus == PlatformSessionStatus.Degraded
            && string.Equals(currentState.BlockedReason, blockedReason, StringComparison.Ordinal)
            && currentState.RetryPhase == AuthRetryPhase.None
            && currentState.AutomaticAttemptNumber == 0
            && currentState.NextRetryAtUtc is null
            && !currentState.RetryLimitReached)
        {
            if (ShouldDispatchDegradedFailureNotification(currentState.CurrentRetryCycleId))
            {
                var replayCorrelationId = CreateCorrelationId();
                await sideEffects.WriteOperationalEventAsync(
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
                await sideEffects.DispatchFailureAsync(currentConfiguration, "IG demo auth is degraded because required credentials are incomplete.", replayCorrelationId, currentState.CurrentRetryCycleId, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId ?? Guid.NewGuid();
        _ = DegradedFailureNotificationsObservedThisProcess.TryAdd(retryCycleId, 0);
        currentState.LatestFailureSummary = blockedReason;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = retryCycleId;
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.Degraded, blockedReason, now));

        await sideEffects.UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, lastDelaySeconds: null, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            "IG demo auth is degraded because required credentials are incomplete.",
            new { RetryCycleId = retryCycleId },
            "Warning",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        await sideEffects.DispatchFailureAsync(currentConfiguration, "IG demo auth is degraded because required credentials are incomplete.", correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task TransitionToDegradedAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        PlatformRuntimeState currentState,
        BrokerAuthenticationFailure failure,
        CancellationToken cancellationToken)
    {
        var failureSummary = failure.Summary;
        var now = timeProvider.GetUtcNow();
        var retryCycleId = currentState.CurrentRetryCycleId ?? Guid.NewGuid();
        var nextDelay = RetryTimingPolicy.CalculateDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);

        _ = DegradedFailureNotificationsObservedThisProcess.TryAdd(retryCycleId, 0);
        currentState.LatestFailureSummary = failureSummary;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = retryCycleId;
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.Degraded, failureSummary, now));

        await sideEffects.UpsertRetryCycleAsync(retryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        await sideEffects.WriteOperationalEventAsync(
            currentConfiguration,
            "auth",
            "FailureDetected",
            failureSummary,
            failure.Diagnostic is null
                ? new { RetryCycleId = retryCycleId }
                : new { RetryCycleId = retryCycleId, Diagnostic = failure.Diagnostic },
            "Warning",
            correlationId,
            retryCycleId,
            cancellationToken).ConfigureAwait(false);
        await sideEffects.DispatchFailureAsync(currentConfiguration, failureSummary, correlationId, retryCycleId, cancellationToken).ConfigureAwait(false);
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
        var nextDelay = RetryTimingPolicy.CalculateDelayBeforeAttempt(currentConfiguration.RetryPolicy, 1);
        var expiredAtUtc = currentState.ExpiresAtUtc;

        const string blockedReason = "The active IG demo session expired and is being re-established.";
        currentState.LatestFailureSummary = blockedReason;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        currentState.RetryLimitReached = false;
        currentState.CurrentRetryCycleId = Guid.NewGuid();
        currentState.LastLoginAttemptAtUtc = now;
        ApplyAuthenticationTransitionOrThrow(
            currentState,
            new AuthenticationStateTransition(PlatformSessionStatus.Degraded, blockedReason, now));

        await sideEffects.UpsertRetryCycleAsync(currentState.CurrentRetryCycleId, currentConfiguration, currentState, "Automatic", failureNotificationSent: true, nextDelay, cancellationToken).ConfigureAwait(false);

        var correlationId = CreateCorrelationId();
        var summary = "The active IG demo session expired and re-authentication started.";

        await sideEffects.WriteOperationalEventAsync(
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

        await sideEffects.DispatchFailureAsync(currentConfiguration, summary, correlationId, currentState.CurrentRetryCycleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IgLoginSnapshot> CaptureSuccessfulLoginSnapshotAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        DateTimeOffset capturedAtUtc,
        BrokerAuthenticationEvidence evidence,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            evidence.AccountId,
            evidence.StreamingEndpoint,
            evidence.ExpiresAtUtc
        });
        var snapshot = new IgLoginSnapshot(
            Guid.NewGuid(),
            currentConfiguration.BrokerEnvironment,
            capturedAtUtc,
            DateOnly.FromDateTime(capturedAtUtc.UtcDateTime),
            IgLoginSnapshotKind.Latest,
            evidence.AccountId,
            evidence.StreamingEndpoint,
            evidence.ExpiresAtUtc,
            new Dictionary<string, string>(),
            payload);
        await igLoginSnapshotStore.CaptureSuccessfulSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);

        await sideEffects.WriteOperationalEventAsync(
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

    private async Task<BrokerAuthenticationOutcome> AuthenticateAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        CancellationToken cancellationToken)
    {
        var authRequest = new BrokerAuthenticationRequest(currentConfiguration.BrokerEnvironment);

        return await brokerAuthenticationGateway
            .AuthenticateAndCollectProofAsync(authRequest, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task CaptureProofDataAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        BrokerAuthenticationProof? proof,
        CancellationToken cancellationToken)
    {
        if (proof is null)
        {
            logger.LogWarning("Broker proof-data query failed; session remains active.");
            return;
        }

        var snapshot = new IgProofDataSnapshot(
            proof.PreferredAccountName,
            proof.PreferredAccountId,
            proof.Balance,
            proof.OpenPositionCount,
            timeProvider.GetUtcNow());
        await igProofDataStore.SaveAsync(currentConfiguration.BrokerEnvironment, snapshot, cancellationToken).ConfigureAwait(false);
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
        return sideEffects.UpsertRetryCycleAsync(
            retryCycleId,
            currentConfiguration,
            currentState,
            cycleType,
            failureNotificationSent,
            lastDelaySeconds,
            cancellationToken);
    }

    private TimeSpan GetSessionLifetime()
    {
        return authSimulationSettings.SessionLifetime;
    }

    private void ApplyAuthenticationTransitionOrThrow(
        PlatformRuntimeState currentState,
        AuthenticationStateTransition transition)
    {
        var result = transitionEngine.Apply(currentState, transition);
        if (!result.IsApplied)
        {
            throw new InvalidOperationException(result.RejectionReason);
        }
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

}
