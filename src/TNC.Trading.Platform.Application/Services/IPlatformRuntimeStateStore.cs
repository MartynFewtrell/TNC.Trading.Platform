using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformRuntimeStateStore
{
    Task<PlatformRuntimeState> GetOrCreateAsync(CancellationToken cancellationToken);

    Task SaveAsync(PlatformRuntimeState state, CancellationToken cancellationToken);
}
