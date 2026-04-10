using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAuthenticateRequest(
    BrokerEnvironmentKind Environment,
    string ApiKey,
    string Identifier,
    string Password);
