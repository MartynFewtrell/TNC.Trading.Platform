using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface IAccountPreferencesCurrentStateStore
{
    Task<AccountPreferencesCurrentState?> GetAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);
    Task<AccountPreferencesDesiredStateCommitResult> CommitDesiredStateAsync(AccountPreferencesDesiredStateChange change, CancellationToken cancellationToken);
    Task<bool> NudgeAuthenticationAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, string authenticationSnapshotId, DateTimeOffset authenticatedAtUtc, DateTimeOffset dueAtUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountPreferencesCurrentState>> ClaimDueWorkAsync(DateTimeOffset nowUtc, int take, CancellationToken cancellationToken);
    Task<AccountPreferencesReconciliationCompletion> CompleteReconciliationAsync(Guid stateId, long desiredRevision, AccountPreferencesCurrentState state, CancellationToken cancellationToken);
}