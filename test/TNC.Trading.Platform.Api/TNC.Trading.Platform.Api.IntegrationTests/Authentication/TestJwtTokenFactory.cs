using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

internal static class TestJwtTokenFactory
{
    public const string Issuer = "https://test-auth.local";

    public const string Audience = "tnc-trading-platform-api";

    public const string SigningKey = "0123456789abcdef0123456789abcdef";

    public static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string url,
        string userName,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> scopes,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null,
        DateTimeOffset? expiresUtc = null,
        DateTimeOffset? notBeforeUtc = null)
    {
        var request = new HttpRequestMessage(method, url);
        var token = CreateToken(
            userName,
            roles,
            scopes,
            issuer,
            audience,
            signingKey,
            expiresUtc,
            notBeforeUtc);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static string CreateToken(
        string userName,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> scopes,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null,
        DateTimeOffset? expiresUtc = null,
        DateTimeOffset? notBeforeUtc = null)
    {
        var claims = new List<Claim>
        {
            new("name", userName),
            new("preferred_username", userName),
            new(ClaimTypes.NameIdentifier, userName),
            new("scope", string.Join(' ', scopes))
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var resolvedSigningKey = signingKey ?? SigningKey;
        var resolvedExpiresUtc = expiresUtc ?? DateTimeOffset.UtcNow.AddHours(1);
        var resolvedNotBeforeUtc = notBeforeUtc
            ?? (resolvedExpiresUtc <= DateTimeOffset.UtcNow
                ? resolvedExpiresUtc.AddMinutes(-5)
                : DateTimeOffset.UtcNow.AddMinutes(-1));
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: claims,
            notBefore: resolvedNotBeforeUtc.UtcDateTime,
            expires: resolvedExpiresUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(resolvedSigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
