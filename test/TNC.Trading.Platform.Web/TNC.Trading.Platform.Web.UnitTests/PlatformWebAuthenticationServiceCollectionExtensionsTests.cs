using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformWebAuthenticationServiceCollectionExtensionsTests
{
    /// <summary>
    /// Trace: FR7, TR2.
    /// Verifies: the shared Web authentication registration creates the viewer, operator, and administrator authorization policies with the expected role matrix.
    /// Expected: each named policy exists and contains a roles requirement aligned to the documented platform role boundaries.
    /// Why: the Web host must apply the same central role-policy catalog everywhere instead of redefining access rules per route.
    /// </summary>
    [Fact]
    public async Task AddPlatformWebAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Test
        });

        builder.AddPlatformWebAuthentication();

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
    /// Trace: NF1, NF3, OR1, SR3.
    /// Verifies: the Web auth registration skips OpenID Connect handler setup when the automated test provider is active.
    /// Expected: the shared cookie scheme is registered and the OpenID Connect scheme is absent.
    /// Why: test-provider runs must remain deterministic and avoid accidental external identity-provider requirements.
    /// </summary>
    [Fact]
    public async Task AddPlatformWebAuthentication_ShouldSkipOpenIdConnectScheme_WhenConfiguredForTestProvider()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Test
        });

        builder.AddPlatformWebAuthentication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        var cookieScheme = await schemeProvider.GetSchemeAsync(PlatformAuthenticationDefaults.Schemes.Cookie);
        var oidcScheme = await schemeProvider.GetSchemeAsync(PlatformAuthenticationDefaults.Schemes.OpenIdConnect);

        Assert.NotNull(cookieScheme);
        Assert.Null(oidcScheme);
    }

    /// <summary>
    /// Trace: NF2, SR1, TR2.
    /// Verifies: the Keycloak Web registration includes the baseline delegated viewer scope in the OpenID Connect sign-in request.
    /// Expected: the resolved OpenID Connect options contain the `platform.viewer` scope.
    /// Why: the Blazor host needs the delegated viewer scope immediately after sign-in so the shared shell can call the protected API without a spurious scope challenge.
    /// </summary>
    [Fact]
    public async Task AddPlatformWebAuthentication_ShouldIncludeViewerScope_WhenConfiguredForKeycloak()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Keycloak,
            ["Authentication:Keycloak:Authority"] = "https://localhost:8080/realms/tnc-trading-platform",
            ["Authentication:Keycloak:ClientId"] = "tnc-trading-platform-web",
            ["Authentication:Keycloak:ApiClientId"] = "tnc-trading-platform-api"
        });

        builder.AddPlatformWebAuthentication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var optionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();

        var options = optionsMonitor.Get(PlatformAuthenticationDefaults.Schemes.OpenIdConnect);

        Assert.Contains(PlatformAuthenticationDefaults.Scopes.Viewer, options.Scope);
    }

    /// <summary>
    /// Trace: FR1, IR1, NF2.
    /// Verifies: the Keycloak Web registration forwards the saved ID token during OpenID Connect sign-out.
    /// Expected: the sign-out redirect event copies the `id_token_hint` value into the outbound protocol message.
    /// Why: ending the provider session requires the Web host to participate in the OIDC sign-out flow instead of clearing only the local cookie.
    /// </summary>
    [Fact]
    public async Task AddPlatformWebAuthentication_ShouldForwardIdTokenHint_WhenSigningOutWithKeycloak()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Keycloak,
            ["Authentication:Keycloak:Authority"] = "https://localhost:8080/realms/tnc-trading-platform",
            ["Authentication:Keycloak:ClientId"] = "tnc-trading-platform-web",
            ["Authentication:Keycloak:ApiClientId"] = "tnc-trading-platform-api"
        });

        builder.AddPlatformWebAuthentication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var optionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        var options = optionsMonitor.Get(PlatformAuthenticationDefaults.Schemes.OpenIdConnect);
        var properties = new AuthenticationProperties();
        properties.Items["id_token_hint"] = "test-id-token";
        var context = new RedirectContext(
            new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider
            },
            new AuthenticationScheme(
                PlatformAuthenticationDefaults.Schemes.OpenIdConnect,
                PlatformAuthenticationDefaults.Schemes.OpenIdConnect,
                typeof(OpenIdConnectHandler)),
            options,
            properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await options.Events.OnRedirectToIdentityProviderForSignOut(context);

        Assert.Equal("test-id-token", context.ProtocolMessage.IdTokenHint);
    }

    /// <summary>
    /// Trace: FR1, FR5, NF2.
    /// Verifies: the Web auth registration forwards an explicit prompt parameter when sign-in should require an interactive credential prompt.
    /// Expected: the sign-in redirect event copies the stored `prompt` value into the outbound OpenID Connect protocol message.
    /// Why: first-open sign-in must not silently reuse an existing provider session when the application requires the operator to authenticate explicitly.
    /// </summary>
    [Fact]
    public async Task AddPlatformWebAuthentication_ShouldForwardPrompt_WhenSignInRequiresInteractiveLogin()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Keycloak,
            ["Authentication:Keycloak:Authority"] = "https://localhost:8080/realms/tnc-trading-platform",
            ["Authentication:Keycloak:ClientId"] = "tnc-trading-platform-web",
            ["Authentication:Keycloak:ApiClientId"] = "tnc-trading-platform-api"
        });

        builder.AddPlatformWebAuthentication();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var optionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        var options = optionsMonitor.Get(PlatformAuthenticationDefaults.Schemes.OpenIdConnect);
        var properties = new AuthenticationProperties();
        properties.Items["prompt"] = "login";
        var context = new RedirectContext(
            new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider
            },
            new AuthenticationScheme(
                PlatformAuthenticationDefaults.Schemes.OpenIdConnect,
                PlatformAuthenticationDefaults.Schemes.OpenIdConnect,
                typeof(OpenIdConnectHandler)),
            options,
            properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };

        await options.Events.OnRedirectToIdentityProvider(context);

        Assert.Equal("login", context.ProtocolMessage.Prompt);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the Web auth registration fails deterministically when the Keycloak provider is selected without an authority.
    /// Expected: resolving the OpenID Connect options throws an invalid-operation error that names the missing Keycloak authority configuration key.
    /// Why: local provider misconfiguration must fail clearly at startup instead of producing an ambiguous sign-in failure later.
    /// </summary>
    [Fact]
    public void AddPlatformWebAuthentication_ShouldThrowInvalidOperationException_WhenKeycloakAuthorityIsMissing()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Keycloak,
            ["Authentication:Keycloak:Authority"] = string.Empty,
            ["Authentication:Keycloak:ClientId"] = "tnc-trading-platform-web"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddPlatformWebAuthentication());

        Assert.Equal(
            "The configuration key 'Authentication:Keycloak:Authority' is required when using the Keycloak provider.",
            exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the Web auth registration fails deterministically when the Keycloak provider is selected without a Web client identifier.
    /// Expected: resolving the OpenID Connect options throws an invalid-operation error that names the missing Keycloak client identifier configuration key.
    /// Why: provider-specific startup validation must identify incomplete Web client wiring before any browser sign-in journey starts.
    /// </summary>
    [Fact]
    public void AddPlatformWebAuthentication_ShouldThrowInvalidOperationException_WhenKeycloakClientIdIsMissing()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = PlatformAuthenticationDefaults.Providers.Keycloak,
            ["Authentication:Keycloak:Authority"] = "http://localhost:8080/realms/tnc-trading-platform",
            ["Authentication:Keycloak:ClientId"] = string.Empty
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddPlatformWebAuthentication());

        Assert.Equal(
            "The configuration key 'Authentication:Keycloak:ClientId' is required when using the Keycloak provider.",
            exception.Message);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR3, SR4.
    /// Verifies: the Web auth registration rejects an unsupported provider selection before runtime authentication handlers are configured.
    /// Expected: registration throws an invalid-operation error that names the unsupported provider value.
    /// Why: startup validation must fail clearly when environment configuration selects an identity provider the application does not support.
    /// </summary>
    [Fact]
    public void AddPlatformWebAuthentication_ShouldThrowInvalidOperationException_WhenProviderIsUnsupported()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "UnsupportedProvider"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddPlatformWebAuthentication());

        Assert.Equal("The authentication provider 'UnsupportedProvider' is not supported.", exception.Message);
    }

    private static WebApplicationBuilder CreateBuilder(IReadOnlyDictionary<string, string?> values)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(values)
        {
            ["Authentication:Authorization:RoleClaimType"] = PlatformAuthenticationDefaults.Claims.Role,
            ["Authentication:Authorization:DisplayNameClaimType"] = PlatformAuthenticationDefaults.Claims.Name,
            ["Authentication:Authorization:DisplayNameFallbackClaimType"] = PlatformAuthenticationDefaults.Claims.PreferredUserName
        });

        return builder;
    }
}
