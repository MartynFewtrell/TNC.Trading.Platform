namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal interface IBrokerAuthenticationGateway
{
    Task<BrokerAuthenticationOutcome> AuthenticateAndCollectProofAsync(
        BrokerAuthenticationRequest request,
        CancellationToken cancellationToken);
}