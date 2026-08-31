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
}