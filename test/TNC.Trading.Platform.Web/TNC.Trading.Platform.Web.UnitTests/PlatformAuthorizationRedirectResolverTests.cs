using System.Security.Claims;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Components.Authorization;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformAuthorizationRedirectResolverTests
{
    /// <summary>
    /// Trace: FR5, TR1.
    /// Verifies: the redirect resolver sends anonymous users to the sign-in entry point and preserves the current protected return URL.
    /// Expected: the resolved destination targets `/authentication/sign-in` and encodes the original route and query string in `returnUrl`.
    /// Why: the protected route pipeline must challenge anonymous users consistently before they can access operator-only UI surfaces.
    /// </summary>
    [Fact]
    public void CreateDecision_ShouldReturnSignInDestination_WhenUserIsAnonymous()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = PlatformAuthorizationRedirectResolver.CreateDecision(
            "https://localhost/status?tab=recent",
            "https://localhost/",
            principal);

        Assert.Equal("/status?tab=recent", result.ReturnUrl);
        Assert.Equal("/authentication/sign-in?returnUrl=%2Fstatus%3Ftab%3Drecent&prompt=login", result.Destination);
        Assert.False(result.ShouldRecordAccessDenied);
    }

    /// <summary>
    /// Trace: FR10, NF4, TR2.
    /// Verifies: the redirect resolver sends authenticated but unauthorized users to the dedicated access-denied route and preserves the original destination.
    /// Expected: the resolved destination targets `/authentication/access-denied` and signals that an access-denied audit event should be recorded.
    /// Why: signed-in underprivileged users must receive a distinct denial experience instead of being treated as anonymous callers.
    /// </summary>
    [Fact]
    public void CreateDecision_ShouldReturnAccessDeniedDestination_WhenUserIsAuthenticated()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PlatformAuthenticationDefaults.Claims.PreferredUserName, "local-viewer")
        ], PlatformAuthenticationDefaults.Schemes.Cookie));

        var result = PlatformAuthorizationRedirectResolver.CreateDecision(
            "https://localhost/administration/authentication",
            "https://localhost/",
            principal);

        Assert.Equal("/administration/authentication", result.ReturnUrl);
        Assert.Equal("/authentication/access-denied?returnUrl=%2Fadministration%2Fauthentication", result.Destination);
        Assert.True(result.ShouldRecordAccessDenied);
    }
}
