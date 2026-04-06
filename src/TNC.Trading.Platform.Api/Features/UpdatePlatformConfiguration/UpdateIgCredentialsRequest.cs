namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdateIgCredentialsRequest(
    string? ApiKey,
    string? Identifier,
    string? Password);
