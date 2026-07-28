namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal sealed record BrokerAuthenticationFailure(
    BrokerAuthenticationFailureKind Kind,
    string Summary);