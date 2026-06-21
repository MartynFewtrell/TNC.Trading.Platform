namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record IgCredentials(
    string ApiKey,
    string Identifier,
    string Password);
