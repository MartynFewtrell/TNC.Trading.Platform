namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record SanitizedIgAuthenticateResponse(
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? ExpiresAtUtc,
    bool HasClientSessionToken,
    bool HasAccountSecurityToken,
    IReadOnlyDictionary<string, string> Headers);
