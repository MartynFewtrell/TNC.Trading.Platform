using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using TNC.Trading.Platform.Application.Authentication;
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

        var cut = context.Render<MainLayout>(parameters =>
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

        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Operator workspace", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Test / Demo", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Status", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Account details", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: account navigation alignment. Verifies Viewer-only and Operator-only principals retain only their authorized account links, while a combined principal places Market categories below Account preferences and above Status.
    /// Expected: each role sees its permitted destination, and the combined navigation preserves the requested order.
    /// Why: moving the navigation item must not widen either authorization boundary or regress the account workflow's discoverability.
    /// </summary>
    [Fact]
    public void Render_ShouldPreserveAccountNavigationVisibilityAndOrder_WhenRoleScopesVary()
    {
        using var viewerContext = new PlatformComponentTestContext(
            "local-viewer",
            [PlatformAuthenticationDefaults.Scopes.Viewer]);
        var viewer = viewerContext.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        Assert.NotEmpty(viewer.FindAll("a[href='/account-details']"));
        Assert.NotEmpty(viewer.FindAll("a[href='/market-categories']"));
        Assert.Empty(viewer.FindAll("a[href='/account-preferences']"));

        using var operatorContext = new PlatformComponentTestContext(
            "local-operator",
            [PlatformAuthenticationDefaults.Scopes.Operator]);
        var operatorLayout = operatorContext.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        Assert.NotEmpty(operatorLayout.FindAll("a[href='/account-preferences']"));

        using var combinedContext = new PlatformComponentTestContext(
            "local-operator",
            [PlatformAuthenticationDefaults.Scopes.Viewer, PlatformAuthenticationDefaults.Scopes.Operator]);
        var combined = combinedContext.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));
        var links = combined.FindAll("nav[aria-label='Primary'] a").ToList();
        var accountDetailsIndex = links.FindIndex(link => link.GetAttribute("href") == "/account-details");
        var marketCategoriesIndex = links.FindIndex(link => link.GetAttribute("href") == "/market-categories");
        var accountPreferencesIndex = links.FindIndex(link => link.GetAttribute("href") == "/account-preferences");
        var statusIndex = links.FindIndex(link => link.GetAttribute("href") == "/status");

        Assert.Equal(accountDetailsIndex + 1, accountPreferencesIndex);
        Assert.Equal(accountPreferencesIndex + 1, marketCategoriesIndex);
        Assert.Equal(marketCategoriesIndex + 1, statusIndex);
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

        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, CreateBody()));

        cut.WaitForElement("button.platform-header__toggle").Click();

        cut.WaitForAssertion(() => Assert.Contains("platform-app-shell--collapsed", cut.Markup, StringComparison.Ordinal));
    }

    private static RenderFragment CreateBody() => builder => builder.AddMarkupContent(0, "<h1>Body</h1>");
}
