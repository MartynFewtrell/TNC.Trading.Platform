using TNC.Trading.Platform.Api.Configuration;

namespace TNC.Trading.Platform.Api.Infrastructure.Ig;

internal sealed record IgAuthenticateRequest(
    BrokerEnvironmentKind Environment,
    string ApiKey,
    string Identifier,
    string Password);

internal sealed record IgAuthenticateResponse(
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? ExpiresAtUtc,
    string? ClientSessionToken,
    string? AccountSecurityToken,
    IReadOnlyDictionary<string, string?> Headers);

internal sealed record SanitizedIgAuthenticateResponse(
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? ExpiresAtUtc,
    bool HasClientSessionToken,
    bool HasAccountSecurityToken,
    IReadOnlyDictionary<string, string> Headers);

internal static class IgAuthenticationResponseSanitizer
{
    public static SanitizedIgAuthenticateResponse Sanitize(IgAuthenticateResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var sanitizedHeaders = response.Headers.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveHeader(pair.Key)
                ? "[redacted]"
                : pair.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        return new SanitizedIgAuthenticateResponse(
            response.CurrentAccountId,
            response.LightstreamerEndpoint,
            response.ExpiresAtUtc,
            !string.IsNullOrWhiteSpace(response.ClientSessionToken),
            !string.IsNullOrWhiteSpace(response.AccountSecurityToken),
            sanitizedHeaders);
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        return headerName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("cst", StringComparison.OrdinalIgnoreCase);
    }
}
