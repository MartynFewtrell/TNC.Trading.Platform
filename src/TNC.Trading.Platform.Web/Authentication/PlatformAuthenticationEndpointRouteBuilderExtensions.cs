using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal static class PlatformAuthenticationEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPlatformAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/authentication/sign-in", SignInAsync)
            .AllowAnonymous();
        endpoints.MapGet("/authentication/sign-out", SignOutAsync)
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> SignInAsync(
        HttpContext httpContext,
        IOptions<PlatformAuthenticationOptions> authenticationOptions,
        TestAuthenticationTokenFactory testAuthenticationTokenFactory,
        ILoggerFactory loggerFactory,
        string? returnUrl,
        string? scope,
        string? user)
    {
        var options = authenticationOptions.Value;
        var logger = loggerFactory.CreateLogger(typeof(PlatformAuthenticationEndpointRouteBuilderExtensions));
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);

        if (string.Equals(options.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                return Results.Content(BuildTestSignInMarkup(safeReturnUrl, scope), "text/html");
            }

            var requestedScopes = string.IsNullOrWhiteSpace(scope)
                ? options.RequiredScopes
                : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var (principal, properties) = testAuthenticationTokenFactory.Create(user, requestedScopes);

            await httpContext.SignInAsync(
                PlatformAuthenticationDefaults.Schemes.Cookie,
                principal,
                properties);

            logger.LogInformation(
                "Local test sign-in completed for {UserName} with scopes {Scopes}",
                user,
                string.Join(", ", requestedScopes));

            return Results.LocalRedirect(safeReturnUrl);
        }

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = safeReturnUrl
        };

        if (!string.IsNullOrWhiteSpace(scope))
        {
            authenticationProperties.Items["platform:scope"] = scope;
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            authenticationProperties.Items["login_hint"] = user;
        }

        logger.LogInformation(
            "OIDC sign-in challenge started for return URL {ReturnUrl} with requested scope {Scope}",
            safeReturnUrl,
            scope ?? string.Join(", ", options.RequiredScopes));

        return Results.Challenge(
            authenticationProperties,
                new[] { PlatformAuthenticationDefaults.Schemes.OpenIdConnect });
    }

    private static async Task<IResult> SignOutAsync(
        HttpContext httpContext,
        IOptions<PlatformAuthenticationOptions> authenticationOptions,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(PlatformAuthenticationEndpointRouteBuilderExtensions));
        await httpContext.SignOutAsync(PlatformAuthenticationDefaults.Schemes.Cookie);
        logger.LogInformation("Platform sign-out completed.");
        return Results.LocalRedirect(NormalizeReturnUrl(authenticationOptions.Value.SignedOutRedirectPath));
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        return returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? returnUrl
            : "/";
    }

    private static string BuildTestSignInMarkup(string returnUrl, string? scope)
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
