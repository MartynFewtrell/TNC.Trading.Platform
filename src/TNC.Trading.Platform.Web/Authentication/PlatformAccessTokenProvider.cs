using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformAccessTokenProvider(
    IHttpContextAccessor httpContextAccessor,
    PlatformAuthAuditClient authAuditClient,
    ILogger<PlatformAccessTokenProvider> logger)
{
    public async Task<string> GetAccessTokenAsync(IReadOnlyCollection<string> requiredScopes, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("The current HTTP context is unavailable.");
        cancellationToken.ThrowIfCancellationRequested();

        var accessToken = await httpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("The current operator session does not contain an access token.");
        }

        var grantedScopes = GetGrantedScopes(httpContext.User, accessToken);
        var missingScopes = requiredScopes
            .Where(scope => !grantedScopes.Contains(scope, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (missingScopes.Length > 0)
        {
            await authAuditClient.RecordTokenAcquisitionFailedAsync(
                httpContext.Request.Path.Value,
                missingScopes,
                accessToken,
                cancellationToken);

            logger.LogWarning(
                "The current operator session is missing delegated scopes {MissingScopes}",
                missingScopes);

            throw new PlatformScopeChallengeRequiredException(missingScopes);
        }

        return accessToken;
    }

    private static IReadOnlyCollection<string> GetGrantedScopes(ClaimsPrincipal principal, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var grantedScopes = PlatformTokenScopeEvaluator.ReadEffectiveScopes(accessToken)
            .ToHashSet(StringComparer.Ordinal);

        if (principal.IsInRole(PlatformAuthenticationDefaults.Roles.Viewer)
            || principal.IsInRole(PlatformAuthenticationDefaults.Roles.Operator)
            || principal.IsInRole(PlatformAuthenticationDefaults.Roles.Administrator))
        {
            grantedScopes.Add(PlatformAuthenticationDefaults.Scopes.Viewer);
        }

        if (principal.IsInRole(PlatformAuthenticationDefaults.Roles.Operator)
            || principal.IsInRole(PlatformAuthenticationDefaults.Roles.Administrator))
        {
            grantedScopes.Add(PlatformAuthenticationDefaults.Scopes.Operator);
        }

        if (principal.IsInRole(PlatformAuthenticationDefaults.Roles.Administrator))
        {
            grantedScopes.Add(PlatformAuthenticationDefaults.Scopes.Administrator);
        }

        return grantedScopes;
    }
}
