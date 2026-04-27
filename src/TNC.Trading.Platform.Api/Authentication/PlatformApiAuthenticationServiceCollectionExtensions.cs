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

        builder.Services.AddAuthorization(PlatformAuthorizationPolicyRegistration.AddPlatformRolePolicies);

        var authenticationOptions = builder.Configuration
            .GetSection(PlatformAuthenticationDefaults.ConfigurationSectionName)
            .Get<PlatformAuthenticationOptions>() ?? new PlatformAuthenticationOptions();

        PlatformAuthenticationConfigurationResolver.ValidateProviderSupported(authenticationOptions.Provider);

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
            ValidAudience = PlatformAuthenticationConfigurationResolver.ResolveAudience(authenticationOptions),
            ValidateIssuer = true,
            ValidIssuer = PlatformAuthenticationConfigurationResolver.ResolveAuthority(authenticationOptions)
        };

        if (string.Equals(authenticationOptions.Provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authenticationOptions.Test.SigningKey));
            options.TokenValidationParameters.ValidIssuer = authenticationOptions.Test.Issuer;
            options.TokenValidationParameters.ValidAudience = authenticationOptions.ApiAudience ?? authenticationOptions.Test.Audience;
            return;
        }

        var authority = PlatformAuthenticationConfigurationResolver.ResolveAuthority(authenticationOptions);
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException("The configured authentication provider authority is missing.");
        }

        options.Authority = authority;
        options.Audience = PlatformAuthenticationConfigurationResolver.ResolveAudience(authenticationOptions);
    }
}
