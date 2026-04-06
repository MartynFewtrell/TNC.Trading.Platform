using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record CredentialPresence(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword)
{
    [JsonIgnore]
    public bool IsComplete => HasApiKey && HasIdentifier && HasPassword;
}
