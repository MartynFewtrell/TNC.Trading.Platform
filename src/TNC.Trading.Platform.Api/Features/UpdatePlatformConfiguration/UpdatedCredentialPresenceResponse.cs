namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatedCredentialPresenceResponse(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword);
