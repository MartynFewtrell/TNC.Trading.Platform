using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformIgLoginSnapshotStore
{
    Task CaptureSuccessfulSnapshotAsync(IgLoginSnapshot latestSnapshot, CancellationToken cancellationToken);

    Task<IgLoginSnapshot?> GetLatestSnapshotAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);

    Task<IReadOnlyList<IgLoginSnapshot>> GetRetainedDailySnapshotsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);
}
