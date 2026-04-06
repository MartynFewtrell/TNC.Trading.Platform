namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAuthenticateResponse(
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? ExpiresAtUtc,
    string? ClientSessionToken,
    string? AccountSecurityToken,
    IReadOnlyDictionary<string, string?> Headers);
