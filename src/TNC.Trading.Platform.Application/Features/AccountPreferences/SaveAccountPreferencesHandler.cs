using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class SaveAccountPreferencesHandler(
    PlatformConfigurationService configurationService,
    IPlatformIgLoginSnapshotStore loginSnapshotStore,
    IAccountPreferencesCurrentStateStore stateStore,
    IAccountPreferencesOperationStore operationStore,
    IAccountPreferencesGateway gateway,
    IAccountPreferencesReconciliationLease lease,
    TimeProvider timeProvider)
{
    public async Task<SaveAccountPreferencesResult> HandleAsync(SaveAccountPreferencesCommand command, string actor, string correlationId, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await loginSnapshotStore.GetLatestSnapshotAsync(configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (snapshot is null) return new(null, AccountPreferencesOperationPhase.VerificationFailed, AccountPreferencesFailureCategory.AccountMismatch, "No authenticated IG account.");

        var existing = await operationStore.FindByIdempotencyKeyAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, command.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.AccountId != snapshot.CurrentAccountId || existing.RequestedTrailingStopsEnabled != command.TrailingStopsEnabled || existing.Actor != actor)
                return new(null, existing.Phase, AccountPreferencesFailureCategory.Rejected, "The idempotency key was already used for different account preferences.", Conflict: true);
            return new(await stateStore.GetAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false), existing.Phase);
        }

        var state = await stateStore.GetAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (state?.AccountId is not null && state.AccountId != snapshot.CurrentAccountId)
            return new(state, AccountPreferencesOperationPhase.VerificationFailed, AccountPreferencesAccountMismatch.Category, AccountPreferencesAccountMismatch.Detail, Conflict: true);
        var baselineRevision = state?.DesiredRevision ?? 0;
        if (command.ExpectedRevision is not null && command.ExpectedRevision != state?.DesiredRevision)
            return new(state, AccountPreferencesOperationPhase.Started, AccountPreferencesFailureCategory.Rejected, "The account preferences revision is stale.", Conflict: true);

        var now = timeProvider.GetUtcNow();
        var operation = new AccountPreferencesOperation(Guid.NewGuid(), command.IdempotencyKey, configuration.PlatformEnvironment, configuration.BrokerEnvironment, snapshot.CurrentAccountId, baselineRevision, command.TrailingStopsEnabled, actor, correlationId, AccountPreferencesOperationPhase.Started, now, now);
        await operationStore.StartAsync(operation, cancellationToken).ConfigureAwait(false);
        await using var acquired = await lease.AcquireAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, snapshot.CurrentAccountId, cancellationToken).ConfigureAwait(false);
        if (acquired is null) return new(state, AccountPreferencesOperationPhase.Started, AccountPreferencesFailureCategory.Rejected, "Account preferences are busy.", Conflict: true);
        var current = await stateStore.GetAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        if (current?.DesiredRevision != state?.DesiredRevision || current?.AccountId != state?.AccountId)
            return new(current, AccountPreferencesOperationPhase.Started, AccountPreferencesFailureCategory.Rejected, "The account preferences revision is stale.", Conflict: true);

        var remote = await gateway.RemediateAsync(new AccountPreferencesRemediateRequest(snapshot.CurrentAccountId, command.TrailingStopsEnabled), cancellationToken).ConfigureAwait(false);
        if (remote.FailureCategory is not null || remote.TrailingStopsEnabled != command.TrailingStopsEnabled)
        {
            var outcomeUnknown = remote.FailureCategory is AccountPreferencesFailureCategory.Unavailable or AccountPreferencesFailureCategory.Transient;
            return new(current, AccountPreferencesOperationPhase.VerificationFailed, outcomeUnknown ? AccountPreferencesFailureCategory.Unavailable : remote.FailureCategory ?? AccountPreferencesFailureCategory.Rejected, outcomeUnknown ? "Save outcome unknown." : remote.SafeReason ?? "IG account preferences update was not confirmed.", OutcomeUnknown: outcomeUnknown);
        }
        await operationStore.SetPhaseAsync(operation.Id, AccountPreferencesOperationPhase.RemoteApplied, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var change = new AccountPreferencesDesiredStateChange(configuration.PlatformEnvironment, configuration.BrokerEnvironment, snapshot.CurrentAccountId, command.TrailingStopsEnabled, command.ExpectedRevision, actor, now, correlationId);
        var observation = new TrailingStopsPreferenceObservation(Guid.NewGuid(), remote.TrailingStopsEnabled!.Value, remote.ObservedAtUtc, timeProvider.GetUtcNow(), configuration.PlatformEnvironment, configuration.BrokerEnvironment, remote.AccountId, "Confirmed", "AccountPreferences", actor, correlationId);
        try
        {
            var commit = await stateStore.FinalizeConfirmedSaveAsync(operation, change, observation, cancellationToken).ConfigureAwait(false);
            return new(commit.State, commit.Committed ? AccountPreferencesOperationPhase.Completed : AccountPreferencesOperationPhase.VerificationFailed, commit.Committed ? null : AccountPreferencesFailureCategory.Rejected, commit.Committed ? null : "The account preferences revision is stale.", !commit.Committed);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(current, AccountPreferencesOperationPhase.RemoteApplied, AccountPreferencesFailureCategory.Unavailable, "Save outcome unknown.", OutcomeUnknown: true);
        }
    }
}