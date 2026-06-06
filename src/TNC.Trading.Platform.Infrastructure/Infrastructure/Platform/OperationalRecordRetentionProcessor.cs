using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class OperationalRecordRetentionProcessor(
    PlatformDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<OperationalRecordRetentionProcessor> logger)
{
    public async Task<int> ApplyAsync(CancellationToken cancellationToken)
    {
        var retentionDays = GetRetentionDays();
        var cutoff = timeProvider.GetUtcNow().AddDays(-retentionDays);

        int deletedCount;

        if (dbContext.Database.IsSqlServer())
        {
            // Set-based delete: no rows loaded into memory — efficient for large tables on SQL Server.
            var deletedEvents = await dbContext.OperationalEvents
                .Where(item => item.OccurredAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            var deletedAudits = await dbContext.ConfigurationAudits
                .Where(item => item.OccurredAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            var deletedNotifications = await dbContext.NotificationRecords
                .Where(item => item.DispatchedAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            var deletedRetainedIgLoginSnapshots = await dbContext.IgLoginSnapshots
                .Where(item => item.SnapshotKind == TNC.Trading.Platform.Application.Configuration.IgLoginSnapshotKind.RetainedDailyFirstSuccessful.ToString()
                    && item.CapturedAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
                .ConfigureAwait(false);

            deletedCount = deletedEvents + deletedAudits + deletedNotifications + deletedRetainedIgLoginSnapshots;
        }
        else
        {
            // Fallback for providers that do not support ExecuteDeleteAsync (e.g., the in-memory
            // database used in unit tests). This path is not reached in production.
            var expiredOperationalEvents = await dbContext.OperationalEvents
                .Where(item => item.OccurredAtUtc < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var expiredConfigurationAudits = await dbContext.ConfigurationAudits
                .Where(item => item.OccurredAtUtc < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var expiredNotificationRecords = await dbContext.NotificationRecords
                .Where(item => item.DispatchedAtUtc < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var expiredRetainedIgLoginSnapshots = await dbContext.IgLoginSnapshots
                .Where(item => item.SnapshotKind == "RetainedDailyFirstSuccessful" && item.CapturedAtUtc < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            deletedCount = expiredOperationalEvents.Count + expiredConfigurationAudits.Count + expiredNotificationRecords.Count + expiredRetainedIgLoginSnapshots.Count;
            if (deletedCount > 0)
            {
                dbContext.OperationalEvents.RemoveRange(expiredOperationalEvents);
                dbContext.ConfigurationAudits.RemoveRange(expiredConfigurationAudits);
                dbContext.NotificationRecords.RemoveRange(expiredNotificationRecords);
                dbContext.IgLoginSnapshots.RemoveRange(expiredRetainedIgLoginSnapshots);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (deletedCount == 0)
        {
            return 0;
        }

        logger.LogInformation(
            "Deleted {DeletedCount} expired operational records using a retention window of {RetentionDays} days.",
            deletedCount,
            retentionDays);

        return deletedCount;
    }

    private int GetRetentionDays()
    {
        var configuredValue = configuration["Retention:OperationalRecordsDays"];
        return int.TryParse(configuredValue, out var retentionDays) && retentionDays > 0
            ? retentionDays
            : 90;
    }
}
