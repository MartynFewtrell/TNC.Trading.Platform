using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using TNC.Trading.Platform.Web.Components.Layout;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class MainLayoutTests
{
    /// <summary>
    /// Trace: FR4, NF2, TR1, OR1.
    /// Verifies: the shared shell renders the signed-out full-width experience when the current operator has no authenticated platform session.
    /// Expected: the main shell body uses the full-width modifier and the primary navigation is not rendered.
    /// Why: the refreshed shell must not show protected navigation affordances before authentication completes.
    /// </summary>
    [Fact]
    public void Render_ShouldUseFullWidthShell_WhenOperatorIsSignedOut()
    {
        using var context = new PlatformComponentTestContext(userName: null);

        var cut = context.RenderComponent<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        Assert.Contains("platform-app-shell__body--full-width", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Primary\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Sign in", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: FR4, NF2, TR1, OR1.
    /// Verifies: the shared shell renders protected navigation and the environment badge once a platform role holder is signed in.
    /// Expected: the primary navigation and the environment summary badge are both visible for a signed-in viewer.
    /// Why: the refreshed shell should expose the current environment context and protected navigation without requiring a browser-driven validation path.
    /// </summary>
    [Fact]
    public void Render_ShouldShowNavigationAndEnvironmentBadge_WhenOperatorHasPlatformRole()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Operator workspace", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Test / Demo", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Status", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR4, NF2, TR1, OR1.
    /// Verifies: the shared shell toggles the sidebar collapse state from the header control.
    /// Expected: selecting the sidebar toggle adds the collapsed shell modifier class.
    /// Why: the refreshed layout must keep the operator navigation behavior observable at the component level instead of only in browser smoke tests.
    /// </summary>
    [Fact]
    public void ToggleSidebar_ShouldCollapseShell_WhenOperatorSelectsSidebarToggle()
    {
        using var context = new PlatformComponentTestContext(
            "local-viewer",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateEvents()));

        var cut = context.RenderComponent<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        cut.WaitForElement("button.platform-header__toggle").Click();

        cut.WaitForAssertion(() => Assert.Contains("platform-app-shell--collapsed", cut.Markup, StringComparison.Ordinal));
    }

    private static RenderFragment CreateBody() => builder => builder.AddMarkupContent(0, "<h1>Body</h1>");
}
