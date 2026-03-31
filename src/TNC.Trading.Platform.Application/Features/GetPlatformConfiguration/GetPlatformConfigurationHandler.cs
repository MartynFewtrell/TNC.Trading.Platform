using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;

internal sealed class GetPlatformConfigurationHandler(PlatformConfigurationService configurationService)
{
    public async Task<GetPlatformConfigurationResponse> HandleAsync(GetPlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        return new GetPlatformConfigurationResponse(configuration);
    }
}
