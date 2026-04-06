using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformRetryCycleStore
{
    Task UpsertAsync(PlatformRetryCycle cycle, CancellationToken cancellationToken);
}
