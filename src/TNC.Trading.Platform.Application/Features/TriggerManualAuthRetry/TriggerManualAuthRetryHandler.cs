using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

internal sealed class TriggerManualAuthRetryHandler(
    PlatformConfigurationService configurationService,
    IPlatformRuntimeStateStore runtimeStateStore,
    TradingScheduleGate tradingScheduleGate,
    IManualAuthRetryCommitter committer,
    IBrokerAuthenticationGateway brokerAuthenticationGateway,
    INotificationDispatcher notificationDispatcher,
    PlatformAuthSimulationSettings authSimulationSettings,
    TimeProvider timeProvider)
{
    public async Task<TriggerManualAuthRetryResponse> HandleAsync(TriggerManualAuthRetryRequest request, CancellationToken cancellationToken)
    {
        var currentState = await runtimeStateStore.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        var configuration = await configurationService.GetRuntimeAsync(
            ParsePlatformEnvironment(currentState.PlatformEnvironment),
            ParseBrokerEnvironment(currentState.BrokerEnvironment),
            cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var scheduleStatus = tradingScheduleGate.Evaluate(configuration.TradingSchedule, now);

        if (!scheduleStatus.IsActive)
        {
            return Rejected(ManualAuthRetryRejectionReason.ScheduleInactive);
        }

        if (TradingScheduleGate.IsLiveTargetBlocked(configuration.PlatformEnvironment, configuration.BrokerEnvironment))
        {
            return Rejected(ManualAuthRetryRejectionReason.BlockedLive);
        }

        if (!currentState.RetryLimitReached || currentState.SessionStatus != PlatformSessionStatus.Degraded)
        {
            return Rejected(ManualAuthRetryRejectionReason.RetryLimitNotReached);
        }

        currentState.PlatformEnvironment = configuration.PlatformEnvironment.ToString();
        currentState.BrokerEnvironment = configuration.BrokerEnvironment.ToString();
        currentState.TradingScheduleStatus = "Active";
        var retryCycleId = Guid.NewGuid();
        var nextDelay = RetryTimingPolicy.CalculateDelayBeforeAttempt(configuration.RetryPolicy, 1);
        currentState.CurrentRetryCycleId = retryCycleId;
        currentState.AutomaticAttemptNumber = 0;
        currentState.RetryPhase = AuthRetryPhase.InitialAutomatic;
        currentState.RetryLimitReached = false;
        currentState.NextRetryAtUtc = now.AddSeconds(nextDelay);
        ApplyDegradedState(currentState, now, "IG demo credentials are incomplete.");

        await committer.CommitAsync(CreateIntent(
            configuration,
            currentState,
            retryCycleId,
            "ManualRetryRequested",
            "Manual retry requested for the current degraded auth cycle.",
            "Information",
            nextDelay), cancellationToken).ConfigureAwait(false);

        var authentication = await brokerAuthenticationGateway.AuthenticateAndCollectProofAsync(
            new BrokerAuthenticationRequest(configuration.BrokerEnvironment),
            cancellationToken).ConfigureAwait(false);

        if (!authentication.IsAuthenticated)
        {
            currentState.LatestFailureSummary = authentication.Failure?.Summary ?? "IG authentication failed.";
            await committer.CommitAsync(CreateIntent(
                configuration,
                currentState,
                retryCycleId,
                "FailureDetected",
                currentState.LatestFailureSummary,
                "Warning",
                nextDelay), cancellationToken).ConfigureAwait(false);
            return new TriggerManualAuthRetryResponse(TriggerManualAuthRetryOutcome.Accepted(retryCycleId));
        }

        var snapshot = CreateSnapshot(configuration, authentication.Evidence!, now);
        currentState.LatestFailureSummary = null;
        currentState.RetryPhase = AuthRetryPhase.None;
        currentState.AutomaticAttemptNumber = 0;
        currentState.NextRetryAtUtc = null;
        currentState.RetryLimitReached = false;
        currentState.LastLoginAttemptAtUtc = now;
        currentState.LastSuccessfulLoginAtUtc = snapshot.CapturedAtUtc;
        currentState.LatestIgLoginSnapshotId = snapshot.Id;
        ApplyActiveState(currentState, now);
        currentState.CurrentRetryCycleId = null;
        await committer.CommitAsync(CreateIntent(
            configuration,
            currentState,
            retryCycleId,
            "Recovered",
            "IG demo auth recovered after manual retry.",
            "Information",
            null,
            snapshot,
            CreateProof(authentication.Proof)), cancellationToken).ConfigureAwait(false);

        await notificationDispatcher.DispatchRecoveryAsync(
            configuration,
            "IG demo auth recovered after manual retry.",
            Guid.NewGuid().ToString("N"),
            retryCycleId,
            cancellationToken).ConfigureAwait(false);

        return new TriggerManualAuthRetryResponse(TriggerManualAuthRetryOutcome.Accepted(retryCycleId));
    }

    private static TriggerManualAuthRetryResponse Rejected(ManualAuthRetryRejectionReason reason) =>
        new(TriggerManualAuthRetryOutcome.Rejected(reason));

    private ManualAuthRetryCommitIntent CreateIntent(
        PlatformConfigurationSnapshot configuration,
        PlatformRuntimeState state,
        Guid retryCycleId,
        string eventType,
        string summary,
        string severity,
        int? lastDelaySeconds,
        IgLoginSnapshot? snapshot = null,
        IgProofDataSnapshot? proof = null)
    {
        var now = timeProvider.GetUtcNow();
        return new(
            configuration,
            state,
            new PlatformRetryCycle
            {
                RetryCycleId = retryCycleId,
                CycleType = "Manual",
                PlatformEnvironment = configuration.PlatformEnvironment.ToString(),
                BrokerEnvironment = configuration.BrokerEnvironment.ToString(),
                RetryPhase = state.RetryPhase,
                AutomaticAttemptNumber = state.AutomaticAttemptNumber,
                NextRetryAtUtc = state.NextRetryAtUtc,
                LastDelaySeconds = lastDelaySeconds,
                PeriodicDelayMinutes = configuration.RetryPolicy.PeriodicDelayMinutes,
                MaxAutomaticRetries = configuration.RetryPolicy.MaxAutomaticRetries,
                RetryLimitReached = state.RetryLimitReached,
                FailureNotificationSent = eventType == "FailureDetected",
                StartedAtUtc = now,
                UpdatedAtUtc = now
            },
            new PlatformEventRecord(
                "auth",
                eventType,
                configuration.PlatformEnvironment,
                configuration.BrokerEnvironment,
                severity,
                summary,
                new { RetryCycleId = retryCycleId },
                Guid.NewGuid().ToString("N"),
                retryCycleId,
                now),
            snapshot,
            proof);
    }

    private static IgLoginSnapshot CreateSnapshot(PlatformConfigurationSnapshot configuration, BrokerAuthenticationEvidence evidence, DateTimeOffset capturedAtUtc)
    {
        var payload = JsonSerializer.Serialize(new { evidence.AccountId, evidence.StreamingEndpoint, evidence.ExpiresAtUtc });
        return new(
            Guid.NewGuid(),
            configuration.BrokerEnvironment,
            capturedAtUtc,
            DateOnly.FromDateTime(capturedAtUtc.UtcDateTime),
            IgLoginSnapshotKind.Latest,
            evidence.AccountId,
            evidence.StreamingEndpoint,
            evidence.ExpiresAtUtc,
            new Dictionary<string, string>(),
            payload);
    }

    private IgProofDataSnapshot? CreateProof(BrokerAuthenticationProof? proof) => proof is null
        ? null
        : new(proof.PreferredAccountName, proof.PreferredAccountId, proof.Balance, proof.OpenPositionCount, timeProvider.GetUtcNow());

    private static void ApplyDegradedState(PlatformRuntimeState state, DateTimeOffset now, string reason)
    {
        state.SessionStatus = PlatformSessionStatus.Degraded;
        state.IsDegraded = true;
        state.BlockedReason = reason;
        state.LastTransitionAtUtc = now;
        state.LastValidatedAtUtc = now;
    }

    private void ApplyActiveState(PlatformRuntimeState state, DateTimeOffset now)
    {
        state.SessionStatus = PlatformSessionStatus.Active;
        state.IsDegraded = false;
        state.BlockedReason = null;
        state.EstablishedAtUtc = now;
        state.ExpiresAtUtc = now.Add(authSimulationSettings.SessionLifetime);
        state.LastTransitionAtUtc = now;
        state.LastValidatedAtUtc = now;
    }

    private static PlatformEnvironmentKind? ParsePlatformEnvironment(string value) =>
        Enum.TryParse<PlatformEnvironmentKind>(value, true, out var parsed) ? parsed : null;

    private static BrokerEnvironmentKind? ParseBrokerEnvironment(string value) =>
        Enum.TryParse<BrokerEnvironmentKind>(value, true, out var parsed) ? parsed : null;
}
