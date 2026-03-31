using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationHandler(
    PlatformConfigurationService configurationService,
    PlatformStateCoordinator coordinator)
{
    public async Task<UpdatePlatformConfigurationResponse> HandleAsync(UpdatePlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        var result = await configurationService.UpdateAsync(request.Update, cancellationToken).ConfigureAwait(false);
        await coordinator.TickAsync(cancellationToken).ConfigureAwait(false);
        return new UpdatePlatformConfigurationResponse(result);
    }
}
