using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Api.UnitTests;

public class PlatformApiAuthenticationServiceCollectionExtensionsTests
{
    private const string AuthenticationExtensionsType = "TNC.Trading.Platform.Api.Authentication.PlatformApiAuthenticationServiceCollectionExtensions";

    /// <summary>
    /// Trace: FR7, TR2.
    /// Verifies: the API authentication registration applies the shared viewer, operator, and administrator role-policy matrix.
    /// Expected: each named API policy exists and contains the documented allowed roles after host registration completes.
    /// Why: shared policy extraction must not change the API host's authorization behavior while removing duplicated registration logic.
    /// </summary>
    [Fact]
    public async Task AddPlatformApiAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Test,
            ["Authentication:Authorization:RoleClaimType"] = PlatformAuthenticationDefaults.Claims.Role,
            ["Authentication:Authorization:DisplayNameClaimType"] = PlatformAuthenticationDefaults.Claims.Name,
            ["Authentication:Authorization:DisplayNameFallbackClaimType"] = PlatformAuthenticationDefaults.Claims.PreferredUserName
        });

        _ = ApiReflection.InvokeStatic(AuthenticationExtensionsType, "AddPlatformApiAuthentication", builder);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var policyProvider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var viewerPolicy = await policyProvider.GetPolicyAsync(PlatformAuthenticationDefaults.Policies.Viewer);
        var operatorPolicy = await policyProvider.GetPolicyAsync(PlatformAuthenticationDefaults.Policies.Operator);
        var administratorPolicy = await policyProvider.GetPolicyAsync(PlatformAuthenticationDefaults.Policies.Administrator);

        Assert.Equal(
            [PlatformAuthenticationDefaults.Roles.Viewer, PlatformAuthenticationDefaults.Roles.Operator, PlatformAuthenticationDefaults.Roles.Administrator],
            Assert.IsType<RolesAuthorizationRequirement>(Assert.Single(viewerPolicy!.Requirements)).AllowedRoles);
        Assert.Equal(
            [PlatformAuthenticationDefaults.Roles.Operator, PlatformAuthenticationDefaults.Roles.Administrator],
            Assert.IsType<RolesAuthorizationRequirement>(Assert.Single(operatorPolicy!.Requirements)).AllowedRoles);
        Assert.Equal(
            [PlatformAuthenticationDefaults.Roles.Administrator],
            Assert.IsType<RolesAuthorizationRequirement>(Assert.Single(administratorPolicy!.Requirements)).AllowedRoles);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the API auth registration rejects an unsupported provider selection before bearer authentication is configured.
    /// Expected: registration throws an invalid-operation error that names the unsupported provider value.
    /// Why: startup validation must fail clearly when environment configuration selects an identity provider the API host does not support.
    /// </summary>
    [Fact]
    public void AddPlatformApiAuthentication_ShouldThrowInvalidOperationException_WhenProviderIsUnsupported()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "UnsupportedProvider"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ApiReflection.InvokeStatic(AuthenticationExtensionsType, "AddPlatformApiAuthentication", builder));

        Assert.Equal("The authentication provider 'UnsupportedProvider' is not supported.", exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the API auth registration fails deterministically when the Keycloak provider is selected without an authority.
    /// Expected: resolving the provider authority throws an invalid-operation error that names the missing Keycloak authority configuration key.
    /// Why: local bearer-token validation must fail clearly when the identity-provider authority has not been configured.
    /// </summary>
    [Fact]
    public void ResolveAuthority_ShouldThrowInvalidOperationException_WhenKeycloakAuthorityIsMissing()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Keycloak,
            Keycloak = new PlatformAuthenticationOptions.KeycloakOptions
            {
                Authority = null,
                ApiClientId = "tnc-trading-platform-api"
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PlatformAuthenticationConfigurationResolver.ResolveAuthority(options));

        Assert.Equal(
            "The configuration key 'Authentication:Keycloak:Authority' is required when using the Keycloak provider.",
            exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the API auth registration fails deterministically when neither an explicit API audience nor a Keycloak API client identifier is configured.
    /// Expected: resolving the API audience throws an invalid-operation error that names the missing Keycloak audience configuration keys.
    /// Why: protected API validation must identify incomplete local bearer-token audience wiring before any request is accepted.
    /// </summary>
    [Fact]
    public void ResolveAudience_ShouldThrowInvalidOperationException_WhenKeycloakApiAudienceIsMissing()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Keycloak,
            ApiAudience = null,
            Keycloak = new PlatformAuthenticationOptions.KeycloakOptions
            {
                Authority = "http://localhost:8080/realms/tnc-trading-platform",
                ApiClientId = string.Empty
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PlatformAuthenticationConfigurationResolver.ResolveAudience(options));

        Assert.Equal(
            "The configuration key 'Authentication:ApiAudience' or 'Authentication:Keycloak:ApiClientId' is required when using the Keycloak provider.",
            exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the API auth registration fails deterministically when the Entra provider is selected without tenant authority inputs.
    /// Expected: resolving the provider authority throws an invalid-operation error that names the missing Entra instance and tenant configuration keys.
    /// Why: Azure-aligned bearer-token validation must not proceed with an incomplete authority definition.
    /// </summary>
    [Fact]
    public void ResolveAuthority_ShouldThrowInvalidOperationException_WhenEntraAuthorityInputsAreMissing()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Entra,
            Entra = new PlatformAuthenticationOptions.EntraOptions
            {
                Instance = string.Empty,
                TenantId = null,
                ApiClientId = "api-client-id"
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PlatformAuthenticationConfigurationResolver.ResolveAuthority(options));

        Assert.Equal(
            "The configuration keys 'Authentication:Entra:Instance' and 'Authentication:Entra:TenantId' are required when using the Entra provider.",
            exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the API auth registration fails deterministically when neither an explicit API audience nor an Entra API client identifier is configured.
    /// Expected: resolving the API audience throws an invalid-operation error that names the missing Entra audience configuration keys.
    /// Why: provider-specific audience validation must fail clearly before the API host starts protecting routes with the wrong resource identifier.
    /// </summary>
    [Fact]
    public void ResolveAudience_ShouldThrowInvalidOperationException_WhenEntraApiAudienceIsMissing()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Entra,
            ApiAudience = null,
            Entra = new PlatformAuthenticationOptions.EntraOptions
            {
                Instance = "https://login.microsoftonline.com/",
                TenantId = "tenant-id",
                ApiClientId = string.Empty
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PlatformAuthenticationConfigurationResolver.ResolveAudience(options));

        Assert.Equal(
            "The configuration key 'Authentication:ApiAudience' or 'Authentication:Entra:ApiClientId' is required when using the Entra provider.",
            exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3.
    /// Verifies: the API auth registration keeps the automated test provider on the local audience and issuer branch without requiring external provider configuration.
    /// Expected: resolving the audience returns the configured test audience and resolving the authority returns the test issuer.
    /// Why: lower-level auth validation coverage must prove the test-provider branch stays isolated from Keycloak or Entra startup requirements.
    /// </summary>
    [Fact]
    public void ResolveAudienceAndAuthority_ShouldUseTestProviderValues_WhenTestProviderIsSelected()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Test,
            ApiAudience = null,
            Test = new PlatformAuthenticationOptions.TestOptions
            {
                Issuer = "https://test-auth.local",
                Audience = "tnc-trading-platform-api"
            }
        };

        var audience = PlatformAuthenticationConfigurationResolver.ResolveAudience(options);
        var authority = PlatformAuthenticationConfigurationResolver.ResolveAuthority(options);

        Assert.Equal("tnc-trading-platform-api", audience);
        Assert.Equal("https://test-auth.local", authority);
    }
}
