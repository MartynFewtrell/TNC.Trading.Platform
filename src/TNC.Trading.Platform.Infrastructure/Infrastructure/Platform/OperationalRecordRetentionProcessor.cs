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

        var deletedCount = expiredOperationalEvents.Count + expiredConfigurationAudits.Count + expiredNotificationRecords.Count;
        if (deletedCount == 0)
        {
            return 0;
        }

        dbContext.OperationalEvents.RemoveRange(expiredOperationalEvents);
        dbContext.ConfigurationAudits.RemoveRange(expiredConfigurationAudits);
        dbContext.NotificationRecords.RemoveRange(expiredNotificationRecords);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
