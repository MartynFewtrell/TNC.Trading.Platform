namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal enum BrokerAuthenticationFailureKind
{
    RejectedCredentials,
    Forbidden,
    RateLimited,
    UnexpectedResponse,
    TimedOut,
    Unreachable,
    MalformedResponse,
    UnsupportedEnvironment
}