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
        PlatformTestAuthenticationSignInHandler testAuthenticationSignInHandler,
        PlatformAuthAuditClient authAuditClient,
        ILoggerFactory loggerFactory,
        string? returnUrl,
        string? scope,
        string? user)
    {
        var options = authenticationOptions.Value;
        var logger = loggerFactory.CreateLogger(typeof(PlatformAuthenticationEndpointRouteBuilderExtensions));
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);

        if (testAuthenticationSignInHandler.IsEnabled)
        {
            return await testAuthenticationSignInHandler.SignInAsync(httpContext, authAuditClient, safeReturnUrl, scope, user);
        }

        if (string.Equals(options.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            logger.LogWarning("Synthetic test sign-in was requested without enabling the explicit Web test harness sign-in surface.");
            return Results.NotFound();
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
        PlatformAuthAuditClient authAuditClient,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(PlatformAuthenticationEndpointRouteBuilderExtensions));
        await authAuditClient.RecordSignOutCompletedAsync("/authentication/sign-out", httpContext.RequestAborted);
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

}
