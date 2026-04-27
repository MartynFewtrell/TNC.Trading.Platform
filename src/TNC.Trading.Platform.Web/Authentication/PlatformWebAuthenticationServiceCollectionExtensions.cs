using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.Authentication;

internal static class PlatformWebAuthenticationServiceCollectionExtensions
{
    public static WebApplicationBuilder AddPlatformWebAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<PlatformAuthenticationOptions>()
            .Bind(builder.Configuration.GetSection(PlatformAuthenticationDefaults.ConfigurationSectionName));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<PlatformOperatorContextAccessor>();
        builder.Services.AddScoped<PlatformNavigationAccessCoordinator>();
        builder.Services.AddScoped<PlatformAccessTokenProvider>();
        builder.Services.AddScoped<TestAuthenticationTokenFactory>();
        builder.Services.AddScoped<PlatformTestAuthenticationSignInHandler>();

        builder.Services.AddAuthorization(PlatformAuthorizationPolicyRegistration.AddPlatformRolePolicies);

        var authenticationOptions = builder.Configuration
            .GetSection(PlatformAuthenticationDefaults.ConfigurationSectionName)
            .Get<PlatformAuthenticationOptions>() ?? new PlatformAuthenticationOptions();

        PlatformAuthenticationConfigurationResolver.ValidateProviderSupported(authenticationOptions.Provider);

        if (!string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            PlatformAuthenticationConfigurationResolver.ValidateOpenIdConnectConfiguration(authenticationOptions);
        }

        var authenticationBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = PlatformAuthenticationDefaults.Schemes.Cookie;
            options.DefaultAuthenticateScheme = PlatformAuthenticationDefaults.Schemes.Cookie;
            options.DefaultSignInScheme = PlatformAuthenticationDefaults.Schemes.Cookie;
            options.DefaultChallengeScheme = PlatformAuthenticationDefaults.Schemes.Cookie;
        });

        authenticationBuilder.AddCookie(
            PlatformAuthenticationDefaults.Schemes.Cookie,
            options =>
            {
                options.AccessDeniedPath = "/authentication/access-denied";
                options.LoginPath = "/authentication/sign-in";
                options.SlidingExpiration = true;
            });

        if (!string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            authenticationBuilder.AddOpenIdConnect(
                PlatformAuthenticationDefaults.Schemes.OpenIdConnect,
                options => ConfigureOpenIdConnectOptions(options, authenticationOptions, builder.Environment));
        }

        return builder;
    }

    private static void ConfigureOpenIdConnectOptions(
        OpenIdConnectOptions options,
        PlatformAuthenticationOptions authenticationOptions,
        IHostEnvironment hostEnvironment)
    {
        var authority = PlatformAuthenticationConfigurationResolver.ResolveAuthority(authenticationOptions);
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException("The configured authentication provider authority is missing.");
        }

        options.Authority = authority;
        options.ClientId = PlatformAuthenticationConfigurationResolver.ResolveClientId(authenticationOptions);
        options.ClientSecret = PlatformAuthenticationConfigurationResolver.ResolveClientSecret(authenticationOptions);
        options.CallbackPath = authenticationOptions.CallbackPath;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !hostEnvironment.IsDevelopment();
        options.GetClaimsFromUserInfoEndpoint = !string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Keycloak, StringComparison.Ordinal);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = authenticationOptions.Authorization.DisplayNameClaimType,
            RoleClaimType = authenticationOptions.Authorization.RoleClaimType
        };

        if (string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Keycloak, StringComparison.Ordinal))
        {
            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
        }

        options.Scope.Clear();
        options.Scope.Add("openid");

        if (!string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Keycloak, StringComparison.Ordinal))
        {
            options.Scope.Add("profile");

            foreach (var scope in authenticationOptions.RequiredScopes)
            {
                options.Scope.Add(scope);
            }
        }

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (!string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Keycloak, StringComparison.Ordinal)
                    && context.Properties.Items.TryGetValue("platform:scope", out var requiredScope)
                    && !string.IsNullOrWhiteSpace(requiredScope))
                {
                    var scopes = new HashSet<string>(StringComparer.Ordinal)
                    {
                        "openid",
                        "profile"
                    };

                    foreach (var scope in requiredScope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        scopes.Add(scope);
                    }

                    foreach (var scope in authenticationOptions.RequiredScopes)
                    {
                        scopes.Add(scope);
                    }

                    context.ProtocolMessage.Scope = string.Join(' ', scopes);
                }

                if (context.Properties.Items.TryGetValue("login_hint", out var loginHint)
                    && !string.IsNullOrWhiteSpace(loginHint))
                {
                    context.ProtocolMessage.LoginHint = loginHint;
                }

                return Task.CompletedTask;
            },
            OnTicketReceived = async context =>
            {
                var auditClient = context.HttpContext.RequestServices.GetRequiredService<PlatformAuthAuditClient>();
                var accessToken = context.Properties?.GetTokenValue("access_token");
                var scope = context.Properties?.GetTokenValue("scope");
                var grantedScopes = string.IsNullOrWhiteSpace(scope)
                    ? authenticationOptions.RequiredScopes
                    : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                await auditClient.RecordSignInCompletedAsync(
                    authenticationOptions.CallbackPath,
                    grantedScopes,
                    accessToken,
                    context.HttpContext.RequestAborted).ConfigureAwait(false);
            }
        };
    }

}
