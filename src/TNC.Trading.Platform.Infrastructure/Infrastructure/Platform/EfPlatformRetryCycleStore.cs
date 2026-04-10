using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class EfPlatformRetryCycleStore(PlatformDbContext dbContext) : IPlatformRetryCycleStore
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
            entity = new AuthRetryCycleEntity
            {
                RetryCycleId = cycle.RetryCycleId,
                StartedAtUtc = cycle.StartedAtUtc,
                CycleType = cycle.CycleType
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
