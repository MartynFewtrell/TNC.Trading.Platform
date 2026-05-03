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
        string? prompt,
        string? user)
    {
        var options = authenticationOptions.Value;
        var logger = loggerFactory.CreateLogger(typeof(PlatformAuthenticationEndpointRouteBuilderExtensions));
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        var challengeReturnUrl = CreateChallengeReturnUrl(safeReturnUrl);

        if (testAuthenticationSignInHandler.IsEnabled)
        {
            return await testAuthenticationSignInHandler.SignInAsync(httpContext, authAuditClient, challengeReturnUrl, scope, user);
        }

        if (string.Equals(options.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            logger.LogWarning("Synthetic test sign-in was requested without enabling the explicit Web test harness sign-in surface.");
            return Results.NotFound();
        }

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = challengeReturnUrl
        };

        if (!string.IsNullOrWhiteSpace(scope))
        {
            authenticationProperties.Items["platform:scope"] = scope;
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            authenticationProperties.Items["login_hint"] = user;
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            authenticationProperties.Items["prompt"] = prompt;
        }

        logger.LogInformation(
            "OIDC sign-in challenge started for return URL {ReturnUrl} with requested scope {Scope} and prompt {Prompt}",
            challengeReturnUrl,
            scope ?? string.Join(", ", options.RequiredScopes),
            prompt ?? "default");

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
        var options = authenticationOptions.Value;
        var logger = loggerFactory.CreateLogger(typeof(PlatformAuthenticationEndpointRouteBuilderExtensions));
        await authAuditClient.RecordSignOutCompletedAsync("/authentication/sign-out", httpContext.RequestAborted);

        var signedOutRedirectPath = NormalizeReturnUrl(options.SignedOutRedirectPath);
        if (string.Equals(options.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            await httpContext.SignOutAsync(PlatformAuthenticationDefaults.Schemes.Cookie);
            logger.LogInformation("Platform sign-out completed for the local test provider.");
            return Results.LocalRedirect(signedOutRedirectPath);
        }

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = signedOutRedirectPath
        };
        var idToken = await httpContext.GetTokenAsync("id_token");
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            authenticationProperties.Items["id_token_hint"] = idToken;
        }

        logger.LogInformation("Platform sign-out completed and the OpenID Connect provider session is being ended.");
        return Results.SignOut(
            authenticationProperties,
            [
                PlatformAuthenticationDefaults.Schemes.Cookie,
                PlatformAuthenticationDefaults.Schemes.OpenIdConnect
            ]);
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

    private static string CreateChallengeReturnUrl(string returnUrl)
    {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{returnUrl}{separator}platformPrompted=1";
    }

}
