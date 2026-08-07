namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal sealed record CredentialPresenceResponse(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword,
    bool IsApiKeyUsable,
    bool IsIdentifierUsable,
    bool IsPasswordUsable,
    bool IsAuthenticationReady,
    bool RequiresCredentialReentry);
