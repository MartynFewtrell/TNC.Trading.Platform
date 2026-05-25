using Bunit;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class HomeTests
{
    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the home page renders the signed-out hero when no operator session is available.
    /// Expected: the sign-in call to action and the public-health hint are visible for an anonymous visitor.
    /// Why: the refreshed landing surface should be covered directly at the component level instead of only through distributed route checks.
    /// </summary>
    [Fact]
    public void Render_ShouldShowSignedOutHero_WhenOperatorIsAnonymous()
    {
        using var context = new PlatformComponentTestContext(userName: null);

        var cut = context.RenderComponent<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Sign in to reach the protected operator surfaces", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Health and readiness endpoints remain public.", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR3, SR1, TR1, OR1.
    /// Verifies: the home page records access denial and redirects authenticated users who hold no platform role.
    /// Expected: the page navigates to the access-denied route and the auth-audit client posts one access-denied audit request.
    /// Why: this refreshed landing-surface behavior is conditional and should be protected by lower-level automated coverage before higher-level auth suites are consolidated.
    /// </summary>
    [Fact]
    public void Render_ShouldRedirectToAccessDenied_WhenAuthenticatedUserHasNoPlatformRole()
    {
        using var context = new PlatformComponentTestContext(userName: "local-norole");

        _ = context.RenderComponent<Home>();

        Assert.Equal("https://localhost/authentication/access-denied", context.NavigationManager.LastNavigationUri);
        var auditRequest = Assert.Single(context.AuditHandler.Requests);
        Assert.EndsWith("/api/platform/auth/audit", auditRequest.RequestUri, StringComparison.Ordinal);
        Assert.Contains("OperatorAccessDenied", auditRequest.Content, StringComparison.Ordinal);
    }
}
