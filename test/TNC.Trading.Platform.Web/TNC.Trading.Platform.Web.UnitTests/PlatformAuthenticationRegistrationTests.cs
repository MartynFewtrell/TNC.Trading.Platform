using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TNC.Trading.Platform.Web.Authentication;
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

    /// <summary>
    /// Trace: FR1, NF2, SR4.
    /// Verifies: the Web entry-session validation rejects a cookie session when the stored delegated access token is missing.
    /// Expected: the evaluation reports the session as unusable.
    /// Why: the UI root must force reauthentication instead of trusting a stale authenticated cookie with no API auth state.
    /// </summary>
    [Fact]
    public void HasUsableSessionToken_ShouldReturnFalse_WhenAccessTokenIsMissing()
    {
        var usable = PlatformTokenScopeEvaluator.HasUsableSessionToken(null);

        Assert.False(usable);
    }

    /// <summary>
    /// Trace: FR1, NF2, SR4.
    /// Verifies: the Web entry-session validation rejects a cookie session when the stored delegated access token is expired.
    /// Expected: the evaluation reports the session as unusable even though token text is present.
    /// Why: the UI root must not render the signed-in shell from stale browser cookie state.
    /// </summary>
    [Fact]
    public void HasUsableSessionToken_ShouldReturnFalse_WhenAccessTokenIsExpired()
    {
        var expiredAccessToken = CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(-5));

        var usable = PlatformTokenScopeEvaluator.HasUsableSessionToken(expiredAccessToken);

        Assert.False(usable);
    }

    /// <summary>
    /// Trace: FR1, NF2, TR3.
    /// Verifies: the Web entry-session validation accepts a current delegated access token.
    /// Expected: the evaluation reports the session as usable when the token is still valid.
    /// Why: the sign-in-first entry rule must preserve valid authenticated sessions instead of prompting unnecessarily.
    /// </summary>
    [Fact]
    public void HasUsableSessionToken_ShouldReturnTrue_WhenAccessTokenIsCurrent()
    {
        var currentAccessToken = CreateAccessToken(DateTimeOffset.UtcNow.AddHours(1));

        var usable = PlatformTokenScopeEvaluator.HasUsableSessionToken(currentAccessToken);

        Assert.True(usable);
    }

    private static string CreateAccessToken(DateTimeOffset expiresUtc)
    {
        var options = new PlatformAuthenticationOptions();
        var notBeforeUtc = expiresUtc <= DateTimeOffset.UtcNow
            ? expiresUtc.AddHours(-1)
            : DateTimeOffset.UtcNow.AddMinutes(-1);
        var claims = new List<Claim>
        {
            new(PlatformAuthenticationDefaults.Claims.Name, "Local Operator"),
            new(PlatformAuthenticationDefaults.Claims.PreferredUserName, "local-operator"),
            new(ClaimTypes.NameIdentifier, "local-operator"),
            new(PlatformAuthenticationDefaults.Claims.Scope, PlatformAuthenticationDefaults.Scopes.Operator),
            new(PlatformAuthenticationDefaults.Claims.Role, PlatformAuthenticationDefaults.Roles.Operator)
        };

        var token = new JwtSecurityToken(
            issuer: options.Test.Issuer,
            audience: options.ApiAudience ?? options.Test.Audience,
            claims: claims,
            notBefore: notBeforeUtc.UtcDateTime,
            expires: expiresUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Test.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
