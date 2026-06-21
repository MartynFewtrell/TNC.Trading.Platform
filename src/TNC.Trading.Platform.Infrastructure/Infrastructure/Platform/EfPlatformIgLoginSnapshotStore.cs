using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class EfPlatformIgLoginSnapshotStore(PlatformDbContext dbContext) : IPlatformIgLoginSnapshotStore
{
    public async Task CaptureSuccessfulSnapshotAsync(IgLoginSnapshot latestSnapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(latestSnapshot);

        var brokerEnvironment = latestSnapshot.BrokerEnvironment.ToString();
        var existingLatestSnapshots = await dbContext.IgLoginSnapshots
            .Where(item => item.BrokerEnvironment == brokerEnvironment && item.SnapshotKind == IgLoginSnapshotKind.Latest.ToString())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existingLatestSnapshots.Count > 0)
        {
            dbContext.IgLoginSnapshots.RemoveRange(existingLatestSnapshots);
        }

        dbContext.IgLoginSnapshots.Add(Map(latestSnapshot));

        var hasRetainedDailySnapshot = await dbContext.IgLoginSnapshots
            .AnyAsync(
                item => item.BrokerEnvironment == brokerEnvironment
                    && item.SnapshotKind == IgLoginSnapshotKind.RetainedDailyFirstSuccessful.ToString()
                    && item.TradingDay == latestSnapshot.TradingDay,
                cancellationToken)
            .ConfigureAwait(false);

        if (!hasRetainedDailySnapshot)
        {
            dbContext.IgLoginSnapshots.Add(Map(latestSnapshot with
            {
                Id = Guid.NewGuid(),
                SnapshotKind = IgLoginSnapshotKind.RetainedDailyFirstSuccessful
            }));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IgLoginSnapshot?> GetLatestSnapshotAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var entity = await dbContext.IgLoginSnapshots
            .AsNoTracking()
            .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString() && item.SnapshotKind == IgLoginSnapshotKind.Latest.ToString())
            .OrderByDescending(item => item.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<IgLoginSnapshot>> GetRetainedDailySnapshotsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var entities = await dbContext.IgLoginSnapshots
            .AsNoTracking()
            .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString() && item.SnapshotKind == IgLoginSnapshotKind.RetainedDailyFirstSuccessful.ToString())
            .OrderByDescending(item => item.TradingDay)
            .ThenByDescending(item => item.CapturedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(Map).ToArray();
    }

    private static IgLoginSnapshotEntity Map(IgLoginSnapshot snapshot)
    {
        return new IgLoginSnapshotEntity
        {
            IgLoginSnapshotId = snapshot.Id,
            BrokerEnvironment = snapshot.BrokerEnvironment.ToString(),
            CapturedAtUtc = snapshot.CapturedAtUtc,
            TradingDay = snapshot.TradingDay,
            SnapshotKind = snapshot.SnapshotKind.ToString(),
            CurrentAccountId = snapshot.CurrentAccountId,
            LightstreamerEndpoint = snapshot.LightstreamerEndpoint,
            SessionExpiresAtUtc = snapshot.SessionExpiresAtUtc,
            ResponseHeadersJson = JsonSerializer.Serialize(snapshot.ResponseHeaders),
            RawNonSecretPayloadJson = snapshot.RawNonSecretPayloadJson
        };
    }

    private static IgLoginSnapshot Map(IgLoginSnapshotEntity entity)
    {
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ResponseHeadersJson)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new IgLoginSnapshot(
            entity.IgLoginSnapshotId,
            Enum.Parse<BrokerEnvironmentKind>(entity.BrokerEnvironment, ignoreCase: true),
            entity.CapturedAtUtc,
            entity.TradingDay,
            Enum.Parse<IgLoginSnapshotKind>(entity.SnapshotKind, ignoreCase: true),
            entity.CurrentAccountId,
            entity.LightstreamerEndpoint,
            entity.SessionExpiresAtUtc,
            headers,
            entity.RawNonSecretPayloadJson);
    }
}
