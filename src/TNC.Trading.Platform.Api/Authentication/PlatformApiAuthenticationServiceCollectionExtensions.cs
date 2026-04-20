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
        authenticationOptions.ApiAudience
        ?? (string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Entra, StringComparison.Ordinal)
            ? authenticationOptions.Entra.ApiClientId ?? throw new InvalidOperationException("The Microsoft Entra API client identifier is missing.")
            : authenticationOptions.Keycloak.ApiClientId);

    private static string? ResolveAuthority(PlatformAuthenticationOptions authenticationOptions) =>
        string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Entra, StringComparison.Ordinal)
            ? ResolveEntraAuthority(authenticationOptions.Entra)
            : authenticationOptions.Keycloak.Authority;

    private static string? ResolveEntraAuthority(PlatformAuthenticationOptions.EntraOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Instance) || string.IsNullOrWhiteSpace(options.TenantId))
        {
            return null;
        }

        return $"{options.Instance.TrimEnd('/')}/{options.TenantId}/v2.0";
    }
}
