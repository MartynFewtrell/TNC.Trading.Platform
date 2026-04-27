using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformTestAuthenticationSignInHandler(
    IOptions<PlatformAuthenticationOptions> authenticationOptions,
    TestAuthenticationTokenFactory testAuthenticationTokenFactory,
    ILogger<PlatformTestAuthenticationSignInHandler> logger)
{
    public bool IsEnabled =>
        string.Equals(authenticationOptions.Value.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal)
        && authenticationOptions.Value.Test.EnableInteractiveSignIn;

    public async Task<IResult> SignInAsync(
        HttpContext httpContext,
        PlatformAuthAuditClient authAuditClient,
        string returnUrl,
        string? scope,
        string? user)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return Results.Content(BuildMarkup(returnUrl, scope), "text/html");
        }

        var requestedScopes = string.IsNullOrWhiteSpace(scope)
            ? authenticationOptions.Value.RequiredScopes
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (principal, properties) = testAuthenticationTokenFactory.Create(user, requestedScopes);

        await httpContext.SignInAsync(
            PlatformAuthenticationDefaults.Schemes.Cookie,
            principal,
            properties);

        await authAuditClient.RecordSignInCompletedAsync(
            "/authentication/sign-in",
            requestedScopes,
            properties.GetTokenValue("access_token"),
            httpContext.RequestAborted);

        logger.LogInformation(
            "Synthetic test sign-in completed for {UserName} with scopes {Scopes}",
            user,
            string.Join(", ", requestedScopes));

        return Results.LocalRedirect(returnUrl);
    }

    private static string BuildMarkup(string returnUrl, string? scope)
    {
        static string CreateLink(string user, string label, string returnUrl, string? scope)
        {
            var url = $"/authentication/sign-in?user={Uri.EscapeDataString(user)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
            if (!string.IsNullOrWhiteSpace(scope))
            {
                url += $"&scope={Uri.EscapeDataString(scope)}";
            }

            return $"<li><a href=\"{url}\">{label}</a></li>";
        }

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>Test sign-in</title>
</head>
<body>
    <h1>Test sign-in</h1>
    <p>Select a local development user to continue.</p>
    <ul>
        {{CreateLink("local-admin", "Sign in as local-admin", returnUrl, scope)}}
        {{CreateLink("local-operator", "Sign in as local-operator", returnUrl, scope)}}
        {{CreateLink("local-viewer", "Sign in as local-viewer", returnUrl, scope)}}
        {{CreateLink("local-norole", "Sign in as local-norole", returnUrl, scope)}}
    </ul>
</body>
</html>
""";
    }
}
