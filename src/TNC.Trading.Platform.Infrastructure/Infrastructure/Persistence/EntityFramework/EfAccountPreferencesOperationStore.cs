using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfAccountPreferencesOperationStore(PlatformDbContext dbContext, IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IAccountPreferencesOperationStore
{
    public async Task<AccountPreferencesOperation?> FindByIdempotencyKeyAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string idempotencyKey, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AccountPreferencesOperations.AsNoTracking().SingleOrDefaultAsync(item => item.PlatformEnvironment == platformEnvironment.ToString() && item.BrokerEnvironment == brokerEnvironment.ToString() && item.IdempotencyKey == idempotencyKey, cancellationToken).ConfigureAwait(false);
        return ToModel(entity);
    }

    public async Task<AccountPreferencesOperation> StartAsync(AccountPreferencesOperation operation, CancellationToken cancellationToken)
    {
        var brokerEnvironmentId = await ResolveBrokerEnvironmentIdAsync(operation.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        var entity = new AccountPreferencesOperationEntity
        {
            AccountPreferencesOperationId = operation.Id,
            BrokerEnvironmentId = brokerEnvironmentId,
            IdempotencyKey = operation.IdempotencyKey,
            PlatformEnvironment = operation.PlatformEnvironment.ToString(),
            BrokerEnvironment = operation.BrokerEnvironment.ToString(),
            AccountId = operation.AccountId,
            BaselineRevision = operation.BaselineRevision,
            RequestedTrailingStopsEnabled = operation.RequestedTrailingStopsEnabled,
            Actor = operation.Actor,
            CorrelationId = operation.CorrelationId,
            Phase = operation.Phase.ToString(),
            CreatedAtUtc = operation.CreatedAtUtc,
            UpdatedAtUtc = operation.UpdatedAtUtc
        };
        dbContext.AccountPreferencesOperations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return operation;
    }

    public async Task<AccountPreferencesOperation?> SetPhaseAsync(Guid operationId, AccountPreferencesOperationPhase phase, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AccountPreferencesOperations.SingleOrDefaultAsync(item => item.AccountPreferencesOperationId == operationId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        entity.Phase = phase.ToString();
        entity.UpdatedAtUtc = updatedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToModel(entity);
    }

    public async Task<IReadOnlyList<AccountPreferencesOperation>> FindRemoteAppliedAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, CancellationToken cancellationToken) =>
        (await dbContext.AccountPreferencesOperations.AsNoTracking().Where(item => item.PlatformEnvironment == platformEnvironment.ToString() && item.BrokerEnvironment == brokerEnvironment.ToString() && item.AccountId == accountId && item.Phase == AccountPreferencesOperationPhase.RemoteApplied.ToString()).OrderBy(item => item.CreatedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(ToModel).Where(item => item is not null).Cast<AccountPreferencesOperation>().ToList();

    private static AccountPreferencesOperation? ToModel(AccountPreferencesOperationEntity? entity) => entity is null ? null : new(entity.AccountPreferencesOperationId, entity.IdempotencyKey, Enum.Parse<PlatformEnvironmentKind>(entity.PlatformEnvironment), Enum.Parse<BrokerEnvironmentKind>(entity.BrokerEnvironment), entity.AccountId, entity.BaselineRevision, entity.RequestedTrailingStopsEnabled, entity.Actor, entity.CorrelationId, Enum.Parse<AccountPreferencesOperationPhase>(entity.Phase), entity.CreatedAtUtc, entity.UpdatedAtUtc);

    private async Task<Guid> ResolveBrokerEnvironmentIdAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var applied = contextResolver is null ? null : await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (applied is not null && string.Equals(applied.Kind, brokerEnvironment.ToString(), StringComparison.OrdinalIgnoreCase)) return applied.BrokerEnvironmentId;

        var brokerEnvironmentId = await dbContext.BrokerEnvironments
            .Where(item => item.Kind == brokerEnvironment.ToString() && item.Availability == "Available")
            .Select(item => (Guid?)item.BrokerEnvironmentId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return brokerEnvironmentId ?? throw new InvalidOperationException($"No available catalog entry exists for the {brokerEnvironment} broker environment.");
    }
}