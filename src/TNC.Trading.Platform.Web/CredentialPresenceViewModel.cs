namespace TNC.Trading.Platform.Web;

internal sealed record CredentialPresenceViewModel(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword);
