using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class EfPlatformEventStore(PlatformDbContext dbContext) : IPlatformEventStore
{
    public async Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken)
    {
        var query = dbContext.OperationalEvents.AsNoTracking().OrderByDescending(item => item.OccurredAtUtc).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(item => item.BrokerEnvironment == environment);
        }

        var events = await query.Take(50).ToListAsync(cancellationToken).ConfigureAwait(false);
        return events
            .Select(item => new OperationalEventModel(
                item.EventId,
                item.Category,
                item.EventType,
                Enum.Parse<PlatformEnvironmentKind>(item.PlatformEnvironment, ignoreCase: true),
                Enum.Parse<BrokerEnvironmentKind>(item.BrokerEnvironment, ignoreCase: true),
                item.Summary,
                item.DetailsJson,
                item.OccurredAtUtc))
            .ToArray();
    }

    public async Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken)
    {
        dbContext.OperationalEvents.Add(new OperationalEventEntity
        {
            Category = record.Category,
            EventType = record.EventType,
            PlatformEnvironment = record.PlatformEnvironment.ToString(),
            BrokerEnvironment = record.BrokerEnvironment.ToString(),
            Severity = record.Severity,
            Summary = record.Summary,
            DetailsJson = OperationalDataRedactor.Serialize(record.Details),
            CorrelationId = record.CorrelationId,
            RetryCycleId = record.RetryCycleId,
            OccurredAtUtc = record.OccurredAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
