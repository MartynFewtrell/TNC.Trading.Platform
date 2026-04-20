using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformAccessTokenProvider(
    IHttpContextAccessor httpContextAccessor,
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

        var grantedScopes = PlatformTokenScopeEvaluator.ReadEffectiveScopes(accessToken);
        var missingScopes = requiredScopes
            .Where(scope => !grantedScopes.Contains(scope, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (missingScopes.Length > 0)
        {
            logger.LogWarning(
                "The current operator session is missing delegated scopes {MissingScopes}",
                missingScopes);

            throw new PlatformScopeChallengeRequiredException(missingScopes);
        }

        return accessToken;
    }
}
