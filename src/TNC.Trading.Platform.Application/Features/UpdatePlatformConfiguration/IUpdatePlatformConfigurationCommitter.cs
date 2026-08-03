using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal interface IUpdatePlatformConfigurationCommitter
{
    Task<UpdatePlatformConfigurationResult> CommitAsync(
        PlatformConfigurationUpdate update,
        CancellationToken cancellationToken);
}