using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformNavigationAccessCoordinator(
    NavigationManager navigationManager,
    PlatformAccessTokenProvider accessTokenProvider,
    PlatformOperatorContextAccessor operatorContextAccessor,
    IOptions<PlatformAuthenticationOptions> authenticationOptions)
{
    public async Task<bool> EnsureRequiredScopesAsync(string returnUrl, params string[] requiredScopes)
    {
        if (requiredScopes.Length == 0)
        {
            return true;
        }

        var operatorContext = await operatorContextAccessor.GetCurrentAsync();
        if (!operatorContext.IsAuthenticated)
        {
            NavigateToSignIn(returnUrl, requiredScopes, null, forcePrompt: true);
            return false;
        }

        try
        {
            _ = await accessTokenProvider.GetAccessTokenAsync(requiredScopes, CancellationToken.None);
            return true;
        }
        catch (PlatformScopeChallengeRequiredException)
        {
            NavigateToSignIn(returnUrl, requiredScopes, operatorContext.UserName);
            return false;
        }
        catch (InvalidOperationException)
        {
            NavigateToSignIn(returnUrl, requiredScopes, operatorContext.UserName);
            return false;
        }
    }

    private void NavigateToSignIn(string returnUrl, IReadOnlyCollection<string> requiredScopes, string? userName, bool forcePrompt = false)
    {
        var scope = string.Join(' ', requiredScopes.Distinct(StringComparer.Ordinal));
        var destination = $"/authentication/sign-in?returnUrl={Uri.EscapeDataString(returnUrl)}&scope={Uri.EscapeDataString(scope)}";
        if (forcePrompt)
        {
            destination += "&prompt=login";
        }

        if (string.Equals(authenticationOptions.Value.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal)
            && authenticationOptions.Value.Test.EnableInteractiveSignIn
            && !string.IsNullOrWhiteSpace(userName))
        {
            destination += $"&user={Uri.EscapeDataString(userName)}";
        }

        navigationManager.NavigateTo(destination, forceLoad: true);
    }
}
