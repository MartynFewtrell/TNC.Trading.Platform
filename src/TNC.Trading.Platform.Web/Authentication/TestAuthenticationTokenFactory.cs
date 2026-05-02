using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class TestAuthenticationTokenFactory(IOptions<PlatformAuthenticationOptions> authenticationOptions)
{
    public (ClaimsPrincipal Principal, AuthenticationProperties Properties) Create(string userName, IReadOnlyCollection<string> requestedScopes)
    {
        var options = authenticationOptions.Value;
        var claims = CreateClaims(userName, options.Authorization.RoleClaimType);
        var token = CreateAccessToken(claims, requestedScopes, options);
        var identity = new ClaimsIdentity(
            claims,
            PlatformAuthenticationDefaults.Schemes.Cookie,
            options.Authorization.DisplayNameClaimType,
            options.Authorization.RoleClaimType);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties();

        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = token }
        ]);

        return (principal, properties);
    }

    private static IReadOnlyList<Claim> CreateClaims(string userName, string roleClaimType)
    {
        var normalizedUser = userName.Trim();
        var roles = normalizedUser switch
        {
            "local-admin" => new[] { PlatformAuthenticationDefaults.Roles.Administrator },
            "local-operator" => new[] { PlatformAuthenticationDefaults.Roles.Operator },
            "local-viewer" => new[] { PlatformAuthenticationDefaults.Roles.Viewer },
            "local-norole" => Array.Empty<string>(),
            _ => throw new InvalidOperationException($"The test authentication user '{normalizedUser}' is not supported.")
        };

        var displayName = normalizedUser switch
        {
            "local-admin" => "Local Administrator",
            "local-operator" => "Local Operator",
            "local-viewer" => "Local Viewer",
            "local-norole" => "Local No Role",
            _ => normalizedUser
        };

        var claims = new List<Claim>
        {
            new(PlatformAuthenticationDefaults.Claims.Name, displayName),
            new(PlatformAuthenticationDefaults.Claims.PreferredUserName, normalizedUser),
            new(ClaimTypes.NameIdentifier, normalizedUser)
        };

        claims.AddRange(roles.Select(role => new Claim(roleClaimType, role)));
        return claims;
    }

    private static string CreateAccessToken(
        IReadOnlyCollection<Claim> claims,
        IReadOnlyCollection<string> requestedScopes,
        PlatformAuthenticationOptions options)
    {
        var scopeSet = new HashSet<string>(requestedScopes, StringComparer.Ordinal)
        {
            PlatformAuthenticationDefaults.Scopes.Viewer
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Test.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var tokenClaims = new List<Claim>(claims)
        {
            new(PlatformAuthenticationDefaults.Claims.Scope, string.Join(' ', scopeSet.OrderBy(scope => scope, StringComparer.Ordinal)))
        };

        var token = new JwtSecurityToken(
            issuer: options.Test.Issuer,
            audience: options.ApiAudience ?? options.Test.Audience,
            claims: tokenClaims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
