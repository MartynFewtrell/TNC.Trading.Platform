using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Api.Authentication;

internal static class PlatformApiAuthenticationServiceCollectionExtensions
{
    public static WebApplicationBuilder AddPlatformApiAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<PlatformAuthenticationOptions>()
            .Bind(builder.Configuration.GetSection(PlatformAuthenticationDefaults.ConfigurationSectionName));

        builder.Services.AddAuthorization(options =>
        {
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
        });

        var authenticationOptions = builder.Configuration
            .GetSection(PlatformAuthenticationDefaults.ConfigurationSectionName)
            .Get<PlatformAuthenticationOptions>() ?? new PlatformAuthenticationOptions();

        ValidateProviderSupported(authenticationOptions.Provider);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = PlatformAuthenticationDefaults.Schemes.Bearer;
            options.DefaultChallengeScheme = PlatformAuthenticationDefaults.Schemes.Bearer;
        }).AddJwtBearer(
            PlatformAuthenticationDefaults.Schemes.Bearer,
            options => ConfigureJwtBearerOptions(options, authenticationOptions, builder.Environment));

        return builder;
    }

    private static void ConfigureJwtBearerOptions(
        JwtBearerOptions options,
        PlatformAuthenticationOptions authenticationOptions,
        IHostEnvironment hostEnvironment)
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !hostEnvironment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = authenticationOptions.Authorization.DisplayNameClaimType,
            RoleClaimType = authenticationOptions.Authorization.RoleClaimType,
            ValidateAudience = true,
            ValidAudience = ResolveAudience(authenticationOptions),
            ValidateIssuer = true,
            ValidIssuer = ResolveAuthority(authenticationOptions)
        };

        if (string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authenticationOptions.Test.SigningKey));
            options.TokenValidationParameters.ValidIssuer = authenticationOptions.Test.Issuer;
            options.TokenValidationParameters.ValidAudience = authenticationOptions.ApiAudience ?? authenticationOptions.Test.Audience;
            return;
        }

        var authority = ResolveAuthority(authenticationOptions);
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException("The configured authentication provider authority is missing.");
        }

        options.Authority = authority;
        options.Audience = ResolveAudience(authenticationOptions);
    }

    private static string ResolveAudience(PlatformAuthenticationOptions authenticationOptions) =>
        !string.IsNullOrWhiteSpace(authenticationOptions.ApiAudience)
            ? authenticationOptions.ApiAudience
            : authenticationOptions.Provider switch
            {
                PlatformAuthenticationDefaults.Providers.Entra => !string.IsNullOrWhiteSpace(authenticationOptions.Entra.ApiClientId)
                    ? authenticationOptions.Entra.ApiClientId
                    : throw new InvalidOperationException("The configuration key 'Authentication:ApiAudience' or 'Authentication:Entra:ApiClientId' is required when using the Entra provider."),
                PlatformAuthenticationDefaults.Providers.Keycloak => !string.IsNullOrWhiteSpace(authenticationOptions.Keycloak.ApiClientId)
                    ? authenticationOptions.Keycloak.ApiClientId
                    : throw new InvalidOperationException("The configuration key 'Authentication:ApiAudience' or 'Authentication:Keycloak:ApiClientId' is required when using the Keycloak provider."),
                PlatformAuthenticationDefaults.Providers.Test => !string.IsNullOrWhiteSpace(authenticationOptions.Test.Audience)
                    ? authenticationOptions.Test.Audience
                    : throw new InvalidOperationException("The configuration key 'Authentication:ApiAudience' or 'Authentication:Test:Audience' is required when using the Test provider."),
                _ => throw new InvalidOperationException($"The authentication provider '{authenticationOptions.Provider}' is not supported.")
            };

    private static string? ResolveAuthority(PlatformAuthenticationOptions authenticationOptions) =>
        authenticationOptions.Provider switch
        {
            PlatformAuthenticationDefaults.Providers.Entra => ResolveEntraAuthority(authenticationOptions.Entra),
            PlatformAuthenticationDefaults.Providers.Keycloak => ResolveKeycloakAuthority(authenticationOptions.Keycloak),
            PlatformAuthenticationDefaults.Providers.Test => authenticationOptions.Test.Issuer,
            _ => throw new InvalidOperationException($"The authentication provider '{authenticationOptions.Provider}' is not supported.")
        };

    private static string? ResolveEntraAuthority(PlatformAuthenticationOptions.EntraOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Instance) || string.IsNullOrWhiteSpace(options.TenantId))
        {
            throw new InvalidOperationException("The configuration keys 'Authentication:Entra:Instance' and 'Authentication:Entra:TenantId' are required when using the Entra provider.");
        }

        return $"{options.Instance.TrimEnd('/')}/{options.TenantId}/v2.0";
    }

    private static string ResolveKeycloakAuthority(PlatformAuthenticationOptions.KeycloakOptions options) =>
        !string.IsNullOrWhiteSpace(options.Authority)
            ? options.Authority
            : throw new InvalidOperationException("The configuration key 'Authentication:Keycloak:Authority' is required when using the Keycloak provider.");

    private static void ValidateProviderSupported(string provider)
    {
        if (string.Equals(provider, PlatformAuthenticationDefaults.Providers.Keycloak, StringComparison.Ordinal)
            || string.Equals(provider, PlatformAuthenticationDefaults.Providers.Entra, StringComparison.Ordinal)
            || string.Equals(provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"The authentication provider '{provider}' is not supported.");
    }
}
