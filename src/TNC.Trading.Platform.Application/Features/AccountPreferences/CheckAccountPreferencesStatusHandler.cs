using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class CheckAccountPreferencesStatusHandler(
    PlatformConfigurationService configurationService,
    IPlatformIgLoginSnapshotStore loginSnapshotStore,
    IAccountPreferencesCurrentStateStore stateStore,
    IAccountPreferencesOperationStore operationStore,
    IAccountPreferencesGateway gateway,
    IAccountPreferencesReconciliationLease lease,
    TimeProvider timeProvider)
{
    public async Task<CheckAccountPreferencesStatusResult> HandleAsync(CheckAccountPreferencesStatusCommand command, string correlationId, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var state = await stateStore.GetAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (state?.DesiredRevision is null || state.AccountId is null) return new(state);
        var snapshot = await loginSnapshotStore.GetLatestSnapshotAsync(configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.CurrentAccountId != state.AccountId) return new(state, AccountPreferencesOperationPhase.VerificationFailed, AccountPreferencesAccountMismatch.Category, AccountPreferencesAccountMismatch.Detail);
        await using var acquired = await lease.AcquireAsync(state.PlatformEnvironment, state.BrokerEnvironment, state.AccountId, cancellationToken).ConfigureAwait(false);
        if (acquired is null) return new(state, AccountPreferencesOperationPhase.Started, AccountPreferencesFailureCategory.Rejected, "Account preferences are busy.", true);
        var current = await stateStore.GetAsync(state.PlatformEnvironment, state.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (current?.DesiredRevision != state.DesiredRevision) return new(current, AccountPreferencesOperationPhase.Started, AccountPreferencesFailureCategory.Rejected, "The account preferences revision is stale.", true);
        var remoteApplied = await operationStore.FindRemoteAppliedAsync(state.PlatformEnvironment, state.BrokerEnvironment, state.AccountId, cancellationToken).ConfigureAwait(false);
        var remote = await gateway.ObserveAsync(new AccountPreferencesObserveRequest(state.AccountId), cancellationToken).ConfigureAwait(false);
        if (remote.FailureCategory is not null) return await FailAsync(current!, remote.FailureCategory.Value, remote.SafeReason, correlationId, cancellationToken).ConfigureAwait(false);
        if (remote.TrailingStopsEnabled != state.DesiredTrailingStopsEnabled)
        {
            var remediation = await gateway.RemediateAsync(new AccountPreferencesRemediateRequest(state.AccountId, state.DesiredTrailingStopsEnabled!.Value), cancellationToken).ConfigureAwait(false);
            if (remediation.FailureCategory is not null || remediation.TrailingStopsEnabled != state.DesiredTrailingStopsEnabled) return await FailAsync(current!, remediation.FailureCategory ?? AccountPreferencesFailureCategory.Rejected, remediation.SafeReason, correlationId, cancellationToken).ConfigureAwait(false);
            remote = new(remote.AccountId, remediation.AttemptId, remediation.ObservedAtUtc, remediation.TrailingStopsEnabled);
        }
        var completed = current! with { ObservedTrailingStopsEnabled = remote.TrailingStopsEnabled, ObservedAccountId = remote.AccountId, ObservedAtUtc = remote.ObservedAtUtc, AttemptId = remote.AttemptId, LastVerifiedAtUtc = remote.ObservedAtUtc, VerificationStatus = AccountPreferencesVerificationStatus.InSync, NextRetryAtUtc = null, RetryCount = 0, FailureSummary = null, CorrelationId = correlationId };
        var observation = new TrailingStopsPreferenceObservation(Guid.NewGuid(), remote.TrailingStopsEnabled!.Value, remote.ObservedAtUtc, timeProvider.GetUtcNow(), state.PlatformEnvironment, state.BrokerEnvironment, remote.AccountId, remoteApplied.Count > 0 ? "Recovered" : "Checked", "AccountPreferences", state.DesiredActor, correlationId);
        var result = await stateStore.CompleteObservedReconciliationAsync(current.Id, current.DesiredRevision.Value, completed, observation, cancellationToken).ConfigureAwait(false);
        return new(result.State ?? current, AccountPreferencesOperationPhase.Completed);
    }

    private async Task<CheckAccountPreferencesStatusResult> FailAsync(AccountPreferencesCurrentState state, AccountPreferencesFailureCategory category, string? reason, string correlationId, CancellationToken cancellationToken)
    {
        var failed = state with { VerificationStatus = AccountPreferencesVerificationStatus.VerificationFailed, FailureSummary = reason is null ? null : reason[..Math.Min(reason.Length, 512)], NextRetryAtUtc = timeProvider.GetUtcNow().AddMinutes(1), RetryCount = state.RetryCount + 1, CorrelationId = correlationId };
        var result = await stateStore.CompleteReconciliationAsync(state.Id, state.DesiredRevision!.Value, failed, cancellationToken).ConfigureAwait(false);
        return new(result.State ?? state, AccountPreferencesOperationPhase.VerificationFailed, category, reason);
    }
}