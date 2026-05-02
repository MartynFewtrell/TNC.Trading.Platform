using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformOperatorContextAccessor(
    AuthenticationStateProvider authenticationStateProvider,
    IOptions<PlatformAuthenticationOptions> authenticationOptions)
{
    public async Task<PlatformOperatorContext> GetCurrentAsync()
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return new PlatformOperatorContext(string.Empty, string.Empty, false, false, false, false, []);
        }

        var options = authenticationOptions.Value.Authorization;
        var roles = user.Claims
            .Where(claim => string.Equals(claim.Type, options.RoleClaimType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var displayName = GetClaimValue(user, options.DisplayNameClaimType)
            ?? GetClaimValue(user, options.DisplayNameFallbackClaimType)
            ?? user.Identity.Name
            ?? string.Empty;
        var userName = GetClaimValue(user, PlatformAuthenticationDefaults.Claims.PreferredUserName)
            ?? user.Identity.Name
            ?? displayName;

        return new PlatformOperatorContext(
            displayName,
            userName,
            true,
            roles.Contains(PlatformAuthenticationDefaults.Roles.Viewer, StringComparer.Ordinal)
            || roles.Contains(PlatformAuthenticationDefaults.Roles.Operator, StringComparer.Ordinal)
            || roles.Contains(PlatformAuthenticationDefaults.Roles.Administrator, StringComparer.Ordinal),
            roles.Contains(PlatformAuthenticationDefaults.Roles.Operator, StringComparer.Ordinal)
            || roles.Contains(PlatformAuthenticationDefaults.Roles.Administrator, StringComparer.Ordinal),
            roles.Contains(PlatformAuthenticationDefaults.Roles.Administrator, StringComparer.Ordinal),
            roles);
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value;
}
