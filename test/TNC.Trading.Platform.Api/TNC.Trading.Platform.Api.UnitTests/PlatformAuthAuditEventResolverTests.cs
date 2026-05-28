using System.Security.Claims;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Api.UnitTests;

public class PlatformAuthAuditEventResolverTests
{
    /// <summary>
    /// Trace: FR3, SR1, TR1.
    /// Verifies: auth-audit record resolution prefers the configured preferred username claim when an operator completes sign-in.
    /// Expected: the resolved audit record uses the preferred username in both the persisted summary and the captured username field.
    /// Why: auth-audit storage must retain the operator identifier that the real runtime exposes most consistently across providers.
    /// </summary>
    [Fact]
    public void TryResolve_ShouldUsePreferredUserName_WhenPreferredUserNameClaimIsPresent()
    {
        var request = new RecordAuthAuditEventRequest(PlatformAuthenticationDefaults.AuditEvents.SignInCompleted, Path: null, Scope: null);
        var user = CreatePrincipal(
            new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "preferred.operator"),
            new Claim(PlatformAuthenticationDefaults.Claims.Name, "display.operator"));

        var resolved = PlatformAuthAuditEventResolver.TryResolve(request, user, out var record);

        Assert.True(resolved);
        Assert.Equal("preferred.operator", record.UserName);
        Assert.Equal("Operator preferred.operator completed sign-in.", record.Summary);
        Assert.Equal("Information", record.Severity);
    }

    /// <summary>
    /// Trace: FR3, SR1, TR1.
    /// Verifies: auth-audit record resolution falls back to the name claim when the preferred username is unavailable for an access-denied event.
    /// Expected: the fallback name is used and the protected-surface placeholder is emitted when no route path is supplied.
    /// Why: denied-access auditing must stay readable even when providers omit the preferred username claim or the caller omits a path value.
    /// </summary>
    [Fact]
    public void TryResolve_ShouldUseNameFallbackAndProtectedSurfacePlaceholder_WhenAccessDeniedPathIsMissing()
    {
        var request = new RecordAuthAuditEventRequest(PlatformAuthenticationDefaults.AuditEvents.AccessDenied, Path: null, Scope: null);
        var user = CreatePrincipal(new Claim(PlatformAuthenticationDefaults.Claims.Name, "display.operator"));

        var resolved = PlatformAuthAuditEventResolver.TryResolve(request, user, out var record);

        Assert.True(resolved);
        Assert.Equal("display.operator", record.UserName);
        Assert.Equal("Operator display.operator was denied access to a protected platform surface.", record.Summary);
        Assert.Equal("Warning", record.Severity);
    }

    /// <summary>
    /// Trace: FR3, SR1, TR1.
    /// Verifies: token-acquisition audit resolution falls back to the authenticated identity name and preserves the missing-scope placeholder.
    /// Expected: the identity name is used as the operator label and the summary points to the requested-scope-set placeholder.
    /// Why: delegated-token failure audits must still produce actionable summaries when upstream claims are sparse.
    /// </summary>
    [Fact]
    public void TryResolve_ShouldUseIdentityNameAndScopePlaceholder_WhenClaimsAreMissing()
    {
        var request = new RecordAuthAuditEventRequest(PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed, Path: null, Scope: null);
        var user = CreatePrincipal(identityName: "identity.operator");

        var resolved = PlatformAuthAuditEventResolver.TryResolve(request, user, out var record);

        Assert.True(resolved);
        Assert.Equal("identity.operator", record.UserName);
        Assert.Equal("Operator identity.operator could not acquire delegated access for the requested scope set.", record.Summary);
        Assert.Equal("Warning", record.Severity);
    }

    /// <summary>
    /// Trace: FR3, NF1, TR1.
    /// Verifies: auth-audit resolution falls back to a deterministic unknown-operator label when no supported identity values are available.
    /// Expected: resolving the username returns the explicit unknown-operator placeholder.
    /// Why: downstream event storage and diagnostics must not receive null or empty operator identifiers.
    /// </summary>
    [Fact]
    public void ResolveUserName_ShouldReturnUnknownOperator_WhenNoIdentityValuesAreAvailable()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var userName = PlatformAuthAuditEventResolver.ResolveUserName(user);

        Assert.Equal("unknown-operator", userName);
    }

    /// <summary>
    /// Trace: FR3, NF1, TR1.
    /// Verifies: auth-audit resolution rejects unsupported event identifiers without fabricating a persisted record.
    /// Expected: the resolver reports failure for an unknown event type.
    /// Why: API validation must continue returning a validation-problem payload instead of recording misleading auth-audit events.
    /// </summary>
    [Fact]
    public void TryResolve_ShouldReturnFalse_WhenEventTypeIsUnsupported()
    {
        var request = new RecordAuthAuditEventRequest("UnsupportedEvent", Path: "/operator/configuration", Scope: null);
        var user = CreatePrincipal(new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "preferred.operator"));

        var resolved = PlatformAuthAuditEventResolver.TryResolve(request, user, out _);

        Assert.False(resolved);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) => CreatePrincipal(null, claims);

    private static ClaimsPrincipal CreatePrincipal(string? identityName, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuthentication");
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            identity = new ClaimsIdentity(claims, authenticationType: "TestAuthentication", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.Name, identityName));
        }

        return new ClaimsPrincipal(identity);
    }
}
