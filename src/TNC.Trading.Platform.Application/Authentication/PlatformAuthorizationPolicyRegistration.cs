using Microsoft.AspNetCore.Authorization;

namespace TNC.Trading.Platform.Application.Authentication;

/// <summary>
/// Registers the shared platform role policies used by the Web and API hosts.
/// </summary>
public static class PlatformAuthorizationPolicyRegistration
{
    /// <summary>
    /// Adds the shared Viewer, Operator, and Administrator authorization policies.
    /// </summary>
    /// <param name="options">The authorization options to configure.</param>
    public static void AddPlatformRolePolicies(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(
            PlatformAuthenticationDefaults.Policies.Viewer,
            policy => policy.RequireRole(
                PlatformAuthenticationDefaults.Roles.Viewer,
                PlatformAuthenticationDefaults.Roles.Operator,
                PlatformAuthenticationDefaults.Roles.Administrator));
        options.AddPolicy(
            PlatformAuthenticationDefaults.Policies.Operator,
            policy => policy.RequireRole(
                PlatformAuthenticationDefaults.Roles.Operator,
                PlatformAuthenticationDefaults.Roles.Administrator));
        options.AddPolicy(
            PlatformAuthenticationDefaults.Policies.Administrator,
            policy => policy.RequireRole(PlatformAuthenticationDefaults.Roles.Administrator));
    }
}
