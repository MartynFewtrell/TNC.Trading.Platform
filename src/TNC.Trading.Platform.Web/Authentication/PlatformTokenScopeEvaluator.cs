using System.IdentityModel.Tokens.Jwt;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal static class PlatformTokenScopeEvaluator
{
    public static IReadOnlyList<string> ReadEffectiveScopes(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            return [];
        }

        var token = handler.ReadJwtToken(accessToken);
        var utcNow = DateTime.UtcNow;
        if (token.ValidFrom > utcNow || token.ValidTo <= utcNow)
        {
            return [];
        }

        var scopes = token.Claims
            .Where(claim => string.Equals(claim.Type, PlatformAuthenticationDefaults.Claims.Scope, StringComparison.Ordinal)
                || string.Equals(claim.Type, PlatformAuthenticationDefaults.Claims.Scp, StringComparison.Ordinal))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var roles = token.Claims
            .Where(claim => string.Equals(claim.Type, PlatformAuthenticationDefaults.Claims.Role, StringComparison.Ordinal)
                || string.Equals(claim.Type, PlatformAuthenticationDefaults.Claims.Roles, StringComparison.Ordinal))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (roles.Any(role => string.Equals(role, PlatformAuthenticationDefaults.Roles.Viewer, StringComparison.Ordinal))
            || roles.Any(role => string.Equals(role, PlatformAuthenticationDefaults.Roles.Operator, StringComparison.Ordinal))
            || roles.Any(role => string.Equals(role, PlatformAuthenticationDefaults.Roles.Administrator, StringComparison.Ordinal)))
        {
            scopes.Add(PlatformAuthenticationDefaults.Scopes.Viewer);
        }

        if (roles.Any(role => string.Equals(role, PlatformAuthenticationDefaults.Roles.Operator, StringComparison.Ordinal))
            || roles.Any(role => string.Equals(role, PlatformAuthenticationDefaults.Roles.Administrator, StringComparison.Ordinal)))
        {
            scopes.Add(PlatformAuthenticationDefaults.Scopes.Operator);
        }

        if (roles.Any(role => string.Equals(role, PlatformAuthenticationDefaults.Roles.Administrator, StringComparison.Ordinal)))
        {
            scopes.Add(PlatformAuthenticationDefaults.Scopes.Administrator);
        }

        return scopes.OrderBy(scope => scope, StringComparer.Ordinal).ToArray();
    }
}
