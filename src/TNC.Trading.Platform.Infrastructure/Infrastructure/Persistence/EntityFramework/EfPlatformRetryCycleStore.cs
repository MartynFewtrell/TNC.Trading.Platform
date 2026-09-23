using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfPlatformRetryCycleStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver appliedBrokerEnvironmentContextResolver) : IPlatformRetryCycleStore
{
    public async Task UpsertAsync(PlatformRetryCycle cycle, CancellationToken cancellationToken)
    {
        var entity = dbContext.AuthRetryCycles.Local
            .FirstOrDefault(item => item.RetryCycleId == cycle.RetryCycleId);

        entity ??= await dbContext.AuthRetryCycles
            .SingleOrDefaultAsync(item => item.RetryCycleId == cycle.RetryCycleId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            var brokerEnvironment = await appliedBrokerEnvironmentContextResolver
                .ResolveAppliedAsync(cancellationToken)
                .ConfigureAwait(false);
            if (brokerEnvironment is null
                || !string.Equals(brokerEnvironment.Kind, cycle.BrokerEnvironment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The applied broker environment must exist and match retry cycle environment '{cycle.BrokerEnvironment}'.");
            }

            entity = new AuthRetryCycleEntity
            {
                RetryCycleId = cycle.RetryCycleId,
                StartedAtUtc = cycle.StartedAtUtc,
                CycleType = cycle.CycleType,
                BrokerEnvironmentId = brokerEnvironment.BrokerEnvironmentId
            };

            dbContext.AuthRetryCycles.Add(entity);
        }

        entity.CycleType = cycle.CycleType;
        entity.PlatformEnvironment = cycle.PlatformEnvironment;
        entity.BrokerEnvironment = cycle.BrokerEnvironment;
        entity.RetryPhase = cycle.RetryPhase.ToString();
        entity.AutomaticAttemptNumber = cycle.AutomaticAttemptNumber;
        entity.NextRetryAtUtc = cycle.NextRetryAtUtc;
        entity.LastDelaySeconds = cycle.LastDelaySeconds;
        entity.PeriodicDelayMinutes = cycle.PeriodicDelayMinutes;
        entity.MaxAutomaticRetries = cycle.MaxAutomaticRetries;
        entity.RetryLimitReached = cycle.RetryLimitReached;
        entity.FailureNotificationSent = cycle.FailureNotificationSent || entity.FailureNotificationSent;
        entity.UpdatedAtUtc = cycle.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
