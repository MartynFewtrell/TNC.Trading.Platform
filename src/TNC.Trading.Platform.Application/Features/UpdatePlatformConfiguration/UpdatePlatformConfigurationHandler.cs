using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationHandler(
    IUpdatePlatformConfigurationCommitter committer,
    ReconcilePlatformAuthenticationHandler reconcileHandler,
    UpdatePlatformConfigurationValidator validator)
{
    public async Task<UpdatePlatformConfigurationResponse> HandleAsync(UpdatePlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        validator.Validate(request.Update);

        var result = await committer.CommitAsync(request.Update, cancellationToken).ConfigureAwait(false);
        await reconcileHandler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), cancellationToken).ConfigureAwait(false);
        return new UpdatePlatformConfigurationResponse(result);
    }
}
