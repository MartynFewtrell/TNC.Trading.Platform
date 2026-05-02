using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformAuthenticationRegistrationTests
{
    /// <summary>
    /// Trace: FR7, TR2.
    /// Verifies: the shared authentication registration layer builds the viewer, operator, and administrator role policies directly.
    /// Expected: the policy catalog contains the documented role matrix without requiring either host-specific registration path.
    /// Why: shared authorization policy extraction must keep the security-sensitive role matrix centralized and host-independent.
    /// </summary>
    [Fact]
    public void AddPlatformRolePolicies_ShouldRegisterExpectedRoleMatrix_WhenCalledDirectly()
    {
        var options = new AuthorizationOptions();

        PlatformAuthorizationPolicyRegistration.AddPlatformRolePolicies(options);

        var viewerPolicy = options.GetPolicy(PlatformAuthenticationDefaults.Policies.Viewer);
        var operatorPolicy = options.GetPolicy(PlatformAuthenticationDefaults.Policies.Operator);
        var administratorPolicy = options.GetPolicy(PlatformAuthenticationDefaults.Policies.Administrator);

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
    /// Trace: NF1, NF3, OR1.
    /// Verifies: the shared configuration resolver returns the configured synthetic-test audience directly when the Test provider is selected.
    /// Expected: the resolved audience matches the configured test audience without requiring host-specific API registration helpers.
    /// Why: shared provider-resolution extraction must preserve the explicit test-harness audience branch used by lower-level auth coverage.
    /// </summary>
    [Fact]
    public void ResolveAudience_ShouldReturnTestAudience_WhenTestProviderIsSelected()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Test,
            Test = new PlatformAuthenticationOptions.TestOptions
            {
                Audience = "tnc-trading-platform-api"
            }
        };

        var audience = PlatformAuthenticationConfigurationResolver.ResolveAudience(options);

        Assert.Equal("tnc-trading-platform-api", audience);
    }

    /// <summary>
    /// Trace: NF1, NF3, OR1, SR4.
    /// Verifies: the shared configuration resolver fails clearly when the Keycloak Web client identifier is missing.
    /// Expected: resolving the client identifier throws an invalid-operation error that names the missing Keycloak client-id configuration key.
    /// Why: shared Web provider-resolution extraction must preserve the startup guard that prevents ambiguous sign-in failures.
    /// </summary>
    [Fact]
    public void ResolveClientId_ShouldThrowInvalidOperationException_WhenKeycloakClientIdIsMissing()
    {
        var options = new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Keycloak,
            Keycloak = new PlatformAuthenticationOptions.KeycloakOptions
            {
                Authority = "http://localhost:8080/realms/tnc-trading-platform",
                ClientId = string.Empty
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PlatformAuthenticationConfigurationResolver.ResolveClientId(options));

        Assert.Equal(
            "The configuration key 'Authentication:Keycloak:ClientId' is required when using the Keycloak provider.",
            exception.Message);
    }
}
