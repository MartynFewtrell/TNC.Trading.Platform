using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry.Ports;

internal interface IManualAuthRetryCommitter
{
    Task CommitAsync(ManualAuthRetryCommitIntent intent, CancellationToken cancellationToken);
}