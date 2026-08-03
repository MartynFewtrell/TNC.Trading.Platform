using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal sealed record BrokerAuthenticationRequest(
    BrokerEnvironmentKind Environment);