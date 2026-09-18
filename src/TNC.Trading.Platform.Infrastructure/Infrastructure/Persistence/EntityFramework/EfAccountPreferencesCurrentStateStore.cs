using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfAccountPreferencesCurrentStateStore(PlatformDbContext dbContext) : IAccountPreferencesCurrentStateStore
{
    public async Task<AccountPreferencesCurrentState?> GetAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) =>
        ToModel(await dbContext.AccountPreferencesCurrentStates.AsNoTracking().SingleOrDefaultAsync(item => item.PlatformEnvironment == platformEnvironment.ToString() && item.BrokerEnvironment == brokerEnvironment.ToString(), cancellationToken).ConfigureAwait(false));

    public async Task<AccountPreferencesDesiredStateCommitResult> CommitDesiredStateAsync(AccountPreferencesDesiredStateChange change, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AccountPreferencesCurrentStates.SingleOrDefaultAsync(item => item.PlatformEnvironment == change.PlatformEnvironment.ToString() && item.BrokerEnvironment == change.BrokerEnvironment.ToString(), cancellationToken).ConfigureAwait(false);
        var previousRevision = entity?.DesiredRevision ?? 0;
        if (change.ExpectedRevision is not null && change.ExpectedRevision != entity?.DesiredRevision) return new(false, previousRevision, ToModel(entity)!);
        var revision = previousRevision + 1;
        var previousValue = entity?.DesiredTrailingStopsEnabled;
        entity ??= new AccountPreferencesCurrentStateEntity { AccountPreferencesCurrentStateId = Guid.NewGuid(), PlatformEnvironment = change.PlatformEnvironment.ToString(), BrokerEnvironment = change.BrokerEnvironment.ToString(), ConcurrencyToken = [] };
        entity.AccountId = change.AccountId; entity.DesiredTrailingStopsEnabled = change.TrailingStopsEnabled; entity.DesiredRevision = revision; entity.DesiredActor = change.Actor; entity.DesiredChangedAtUtc = change.ChangedAtUtc; entity.VerificationStatus = AccountPreferencesVerificationStatus.Pending.ToString(); entity.CorrelationId = change.CorrelationId;
        if (dbContext.Entry(entity).State == EntityState.Detached) dbContext.AccountPreferencesCurrentStates.Add(entity);
        dbContext.AccountPreferencesDesiredStateAudits.Add(new AccountPreferencesDesiredStateAuditEntity { AccountPreferencesDesiredStateAuditId = Guid.NewGuid(), AccountPreferencesCurrentStateId = entity.AccountPreferencesCurrentStateId, PlatformEnvironment = entity.PlatformEnvironment, BrokerEnvironment = entity.BrokerEnvironment, AccountId = change.AccountId, PreviousValue = previousValue, NewValue = change.TrailingStopsEnabled, PreviousRevision = previousRevision, NewRevision = revision, OccurredAtUtc = change.ChangedAtUtc, Actor = change.Actor, ChangeType = "DesiredStateChanged", CorrelationId = change.CorrelationId });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, revision, ToModel(entity)!);
    }

    public async Task<AccountPreferencesDesiredStateCommitResult> FinalizeConfirmedSaveAsync(AccountPreferencesOperation operation, AccountPreferencesDesiredStateChange change, TrailingStopsPreferenceObservation observation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var result = await CommitDesiredStateAsync(change, cancellationToken).ConfigureAwait(false);
        if (!result.Committed) return result;
        var entity = await dbContext.AccountPreferencesCurrentStates.SingleAsync(item => item.AccountPreferencesCurrentStateId == result.State.Id, cancellationToken).ConfigureAwait(false);
        entity.ObservedTrailingStopsEnabled = observation.TrailingStopsEnabled; entity.ObservedAccountId = observation.AccountId; entity.ObservedAtUtc = observation.ObservedAtUtc; entity.AttemptId = observation.Id.ToString("N"); entity.LastVerifiedAtUtc = observation.ObservedAtUtc; entity.VerificationStatus = AccountPreferencesVerificationStatus.InSync.ToString(); entity.FailureSummary = null; entity.NextRetryAtUtc = null; entity.RetryCount = 0;
        dbContext.TrailingStopsPreferenceObservations.Add(ToEntity(observation));
        var journal = await dbContext.AccountPreferencesOperations.SingleAsync(item => item.AccountPreferencesOperationId == operation.Id, cancellationToken).ConfigureAwait(false);
        journal.Phase = AccountPreferencesOperationPhase.Completed.ToString(); journal.UpdatedAtUtc = observation.RecordedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(true, result.Revision, ToModel(entity)!);
    }

    public async Task<AccountPreferencesReconciliationCompletion> CompleteObservedReconciliationAsync(Guid stateId, long desiredRevision, AccountPreferencesCurrentState state, TrailingStopsPreferenceObservation observation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var result = await CompleteReconciliationAsync(stateId, desiredRevision, state, cancellationToken).ConfigureAwait(false);
        if (!result.Applied) return result;
        dbContext.TrailingStopsPreferenceObservations.Add(ToEntity(observation));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<bool> NudgeAuthenticationAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, string authenticationSnapshotId, DateTimeOffset authenticatedAtUtc, DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AccountPreferencesCurrentStates.SingleOrDefaultAsync(item => item.PlatformEnvironment == platformEnvironment.ToString() && item.BrokerEnvironment == brokerEnvironment.ToString() && item.AccountId == accountId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return false;
        entity.AuthenticationSnapshotId = authenticationSnapshotId;
        entity.NextRetryAtUtc = dueAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<AccountPreferencesCurrentState>> ClaimDueWorkAsync(DateTimeOffset nowUtc, int take, CancellationToken cancellationToken) =>
        (await dbContext.AccountPreferencesCurrentStates.AsNoTracking().Where(item => item.DesiredRevision != null && item.NextRetryAtUtc <= nowUtc).OrderBy(item => item.NextRetryAtUtc).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(item => ToModel(item)!).ToList();

    public async Task<AccountPreferencesReconciliationCompletion> CompleteReconciliationAsync(Guid stateId, long desiredRevision, AccountPreferencesCurrentState state, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AccountPreferencesCurrentStates.SingleOrDefaultAsync(item => item.AccountPreferencesCurrentStateId == stateId && item.DesiredRevision == desiredRevision, cancellationToken).ConfigureAwait(false);
        if (entity is null) return new(false, null);
        entity.ObservedTrailingStopsEnabled = state.ObservedTrailingStopsEnabled; entity.ObservedAccountId = state.ObservedAccountId; entity.ObservedAtUtc = state.ObservedAtUtc; entity.AuthenticationSnapshotId = state.AuthenticationSnapshotId; entity.AttemptId = state.AttemptId; entity.VerificationStatus = state.VerificationStatus.ToString(); entity.LastVerifiedAtUtc = state.LastVerifiedAtUtc; entity.NextRetryAtUtc = state.NextRetryAtUtc; entity.RetryCount = state.RetryCount; entity.FailureSummary = state.FailureSummary; entity.CorrelationId = state.CorrelationId;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); return new(true, ToModel(entity));
    }

    private static AccountPreferencesCurrentState? ToModel(AccountPreferencesCurrentStateEntity? item) => item is null ? null : new(item.AccountPreferencesCurrentStateId, Enum.Parse<PlatformEnvironmentKind>(item.PlatformEnvironment), Enum.Parse<BrokerEnvironmentKind>(item.BrokerEnvironment), item.AccountId, item.DesiredTrailingStopsEnabled, item.DesiredRevision, item.DesiredActor, item.DesiredChangedAtUtc, item.ObservedTrailingStopsEnabled, item.ObservedAccountId, item.ObservedAtUtc, item.AuthenticationSnapshotId, item.AttemptId, Enum.Parse<AccountPreferencesVerificationStatus>(item.VerificationStatus), item.LastVerifiedAtUtc, item.NextRetryAtUtc, item.RetryCount, item.FailureSummary, item.CorrelationId, item.ConcurrencyToken);

    private static TrailingStopsPreferenceObservationEntity ToEntity(TrailingStopsPreferenceObservation observation) => new() { TrailingStopsPreferenceObservationId = observation.Id, TrailingStopsEnabled = observation.TrailingStopsEnabled, ObservedAtUtc = observation.ObservedAtUtc, RecordedAtUtc = observation.RecordedAtUtc, PlatformEnvironment = observation.PlatformEnvironment.ToString(), BrokerEnvironment = observation.BrokerEnvironment.ToString(), AccountId = observation.AccountId, ObservationKind = observation.ObservationKind, Source = observation.Source, Actor = observation.Actor, CorrelationId = observation.CorrelationId };
}