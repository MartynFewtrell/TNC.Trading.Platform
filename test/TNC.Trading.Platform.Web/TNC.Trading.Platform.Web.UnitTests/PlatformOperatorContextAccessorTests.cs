using System.Security.Claims;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformOperatorContextAccessorTests
{
    /// <summary>
    /// Trace: FR4, FR8, NF2, TR1.
    /// Verifies: the operator context accessor returns an anonymous context when the current user is not authenticated.
    /// Expected: the returned context exposes no display name, no username, and no platform-role capability flags.
    /// Why: protected UI behavior must fail closed when there is no authenticated operator session.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldReturnAnonymousContext_WhenUserIsNotAuthenticated()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var accessor = CreateAccessor(principal);

        var result = await accessor.GetCurrentAsync();

        Assert.False(result.IsAuthenticated);
        Assert.False(result.HasAnyPlatformRole);
        Assert.False(result.IsViewer);
        Assert.False(result.IsOperator);
        Assert.False(result.IsAdministrator);
        Assert.Equal(string.Empty, result.DisplayName);
        Assert.Equal(string.Empty, result.UserName);
        Assert.Empty(result.Roles);
    }

    /// <summary>
    /// Trace: FR3, FR8, FR10, TR2.
    /// Verifies: an authenticated principal with no platform role remains authenticated but does not gain any protected capability flags.
    /// Expected: the returned context exposes the authenticated identity details while reporting no viewer, operator, or administrator access.
    /// Why: the platform allows a no-role user to authenticate only far enough to route them to the access-denied experience.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldReturnAuthenticatedNoRoleContext_WhenPrincipalHasNoPlatformRole()
    {
        var principal = CreatePrincipal(
            new Claim(PlatformAuthenticationDefaults.Claims.Name, "Local No Role"),
            new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "local-norole"));
        var accessor = CreateAccessor(principal);

        var result = await accessor.GetCurrentAsync();

        Assert.True(result.IsAuthenticated);
        Assert.False(result.HasAnyPlatformRole);
        Assert.False(result.IsViewer);
        Assert.False(result.IsOperator);
        Assert.False(result.IsAdministrator);
        Assert.Equal("Local No Role", result.DisplayName);
        Assert.Equal("local-norole", result.UserName);
        Assert.Empty(result.Roles);
    }

    /// <summary>
    /// Trace: FR8, TR1.
    /// Verifies: the operator context accessor falls back to the configured username claim when the primary display-name claim is absent.
    /// Expected: the returned operator context uses preferred_username for display and marks the viewer capability correctly.
    /// Why: protected UI state must stay understandable even when only the minimum fallback identity claim is available.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldUseFallbackDisplayName_WhenPrimaryDisplayNameClaimIsMissing()
    {
        var principal = CreatePrincipal(
            new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "local-viewer"),
            new Claim(PlatformAuthenticationDefaults.Claims.Role, PlatformAuthenticationDefaults.Roles.Viewer));
        var accessor = CreateAccessor(principal);

        var result = await accessor.GetCurrentAsync();

        Assert.Equal("local-viewer", result.DisplayName);
        Assert.Equal("local-viewer", result.UserName);
        Assert.True(result.IsViewer);
        Assert.False(result.IsOperator);
        Assert.False(result.IsAdministrator);
    }

    /// <summary>
    /// Trace: FR7, FR8, FR9, TR2.
    /// Verifies: the operator context accessor maps administrator role claims into the higher-privilege capability flags.
    /// Expected: an administrator principal is treated as authenticated, viewer-capable, operator-capable, and administrator-capable.
    /// Why: the Blazor UI relies on these derived flags to keep role boundaries consistent across navigation and protected surfaces.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldSetElevatedFlags_WhenAdministratorRoleIsPresent()
    {
        var principal = CreatePrincipal(
            new Claim(PlatformAuthenticationDefaults.Claims.Name, "Local Administrator"),
            new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "local-admin"),
            new Claim(PlatformAuthenticationDefaults.Claims.Role, PlatformAuthenticationDefaults.Roles.Administrator));
        var accessor = CreateAccessor(principal);

        var result = await accessor.GetCurrentAsync();

        Assert.True(result.IsAuthenticated);
        Assert.True(result.HasAnyPlatformRole);
        Assert.True(result.IsViewer);
        Assert.True(result.IsOperator);
        Assert.True(result.IsAdministrator);
    }

    /// <summary>
    /// Trace: FR4, FR7, FR8, FR9, TR2.
    /// Verifies: the operator context accessor honors configured claim mappings and removes duplicate role claims when deriving the operator capability set.
    /// Expected: a principal using custom role and display-name claim types produces a fallback display name and distinct Viewer and Operator roles.
    /// Why: provider-specific claim mappings must remain environment-driven without duplicating platform-role state in the UI.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldHonorConfiguredClaimTypes_WhenCustomRoleAndDisplayNameClaimsAreUsed()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("fallback_display", "Custom Operator"),
            new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "custom-operator"),
            new Claim("custom_role", PlatformAuthenticationDefaults.Roles.Viewer),
            new Claim("custom_role", PlatformAuthenticationDefaults.Roles.Viewer),
            new Claim("custom_role", PlatformAuthenticationDefaults.Roles.Operator)
        ],
            authenticationType: PlatformAuthenticationDefaults.Schemes.Cookie,
            nameType: PlatformAuthenticationDefaults.Claims.Name,
            roleType: "custom_role"));
        var accessor = CreateAccessor(
            principal,
            new PlatformAuthenticationOptions
            {
                Authorization = new PlatformAuthenticationOptions.AuthorizationOptions
                {
                    RoleClaimType = "custom_role",
                    DisplayNameClaimType = "primary_display",
                    DisplayNameFallbackClaimType = "fallback_display"
                }
            });

        var result = await accessor.GetCurrentAsync();

        Assert.True(result.IsAuthenticated);
        Assert.True(result.IsViewer);
        Assert.True(result.IsOperator);
        Assert.False(result.IsAdministrator);
        Assert.Equal("Custom Operator", result.DisplayName);
        Assert.Equal("custom-operator", result.UserName);
        Assert.Equal([PlatformAuthenticationDefaults.Roles.Viewer, PlatformAuthenticationDefaults.Roles.Operator], result.Roles);
    }

    private static PlatformOperatorContextAccessor CreateAccessor(ClaimsPrincipal principal, PlatformAuthenticationOptions? options = null)
    {
        return new PlatformOperatorContextAccessor(
            new TestAuthenticationStateProvider(principal),
            Options.Create(options ?? new PlatformAuthenticationOptions()));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: PlatformAuthenticationDefaults.Schemes.Cookie,
            nameType: PlatformAuthenticationDefaults.Claims.Name,
            roleType: PlatformAuthenticationDefaults.Claims.Role);

        return new ClaimsPrincipal(identity);
    }
}
