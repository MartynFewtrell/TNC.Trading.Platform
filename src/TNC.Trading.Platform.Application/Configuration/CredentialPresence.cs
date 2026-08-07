using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record CredentialPresence(
    bool HasApiKey,
    bool HasIdentifier,
    bool HasPassword,
    bool IsApiKeyUsable = true,
    bool IsIdentifierUsable = true,
    bool IsPasswordUsable = true)
{
    [JsonIgnore]
    public bool IsComplete => HasApiKey && HasIdentifier && HasPassword;

    [JsonIgnore]
    public bool IsAuthenticationReady =>
        IsComplete && IsApiKeyUsable && IsIdentifierUsable && IsPasswordUsable;

    [JsonIgnore]
    public bool RequiresCredentialReentry => IsComplete && !IsAuthenticationReady;
}
