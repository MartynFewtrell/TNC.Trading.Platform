using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface IAccountPreferencesOperationStore
{
    Task<AccountPreferencesOperation?> FindByIdempotencyKeyAsync(
        PlatformEnvironmentKind platformEnvironment,
        BrokerEnvironmentKind brokerEnvironment,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<AccountPreferencesOperation> StartAsync(
        AccountPreferencesOperation operation,
        CancellationToken cancellationToken);

    Task<AccountPreferencesOperation?> SetPhaseAsync(
        Guid operationId,
        AccountPreferencesOperationPhase phase,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountPreferencesOperation>> FindRemoteAppliedAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, CancellationToken cancellationToken);
}