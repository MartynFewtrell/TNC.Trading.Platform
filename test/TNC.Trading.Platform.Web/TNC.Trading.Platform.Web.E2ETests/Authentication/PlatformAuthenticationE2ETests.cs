using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

[Collection(AuthenticationE2ETestCollection.Name)]
public class PlatformAuthenticationE2ETests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true
    };

    static PlatformAuthenticationE2ETests()
    {
        Environment.SetEnvironmentVariable("AppHost__UseSyntheticRuntime", bool.TrueString);
        Environment.SetEnvironmentVariable("Authentication__Test__EnableInteractiveSignIn", bool.TrueString);
    }

    /// <summary>
    /// Trace: FR1, FR2, TR3, OR2.
    /// Verifies: the browser can reach the explicit synthetic Web test harness sign-in entry surface used for automated authentication flows.
    /// Expected: navigating to the sign-in route shows the local test sign-in choices.
    /// Why: the end-to-end suite needs a stable auth entry surface before it can validate later signed-in flows.
    /// </summary>
    [Fact]
    public async Task SignInPage_ShouldShowSyntheticUserChoices_WhenAnonymousBrowserRequestsSignIn()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await Page.GotoAsync(
            new Uri(app.GetEndpoint("web"), "/authentication/sign-in?returnUrl=%2Fstatus").ToString(),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Test sign-in" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("local-viewer")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Trace: FR1, FR5, TR3.
    /// Verifies: opening the application root in a fresh browser session immediately sends the operator to the sign-in experience.
    /// Expected: navigating to `/` in the synthetic test runtime ends on the test sign-in page instead of the anonymous landing content.
    /// Why: the application must prompt for sign-in as soon as it is first opened.
    /// </summary>
    [Fact]
    public async Task LandingPage_ShouldRedirectToSignIn_WhenAnonymousBrowserOpensApp()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await Page.GotoAsync(
            new Uri(app.GetEndpoint("web"), "/").ToString(),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Test sign-in" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Trace: FR1, TR3, NF2.
    /// Verifies: opening the app in a fresh browser tab prompts for sign-in again even when the browser still holds a valid platform session cookie.
    /// Expected: after a seeded viewer signs in on one page, opening `/` in a new page within the same browser context lands on the test sign-in page.
    /// Why: the app must require an explicit sign-in prompt whenever it is first opened instead of silently continuing from the last authenticated session.
    /// </summary>
    [Fact]
    public async Task LandingPage_ShouldPromptForSignIn_WhenAuthenticatedBrowserOpensNewPage()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var rootUri = new Uri(app.GetEndpoint("web"), "/").ToString();
        await Page.GotoAsync(
            new Uri(app.GetEndpoint("web"), "/authentication/sign-in?user=local-viewer&returnUrl=%2F").ToString(),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.GetByText("Signed in as")).ToBeVisibleAsync();

        var secondPage = await Context.NewPageAsync();
        await secondPage.GotoAsync(rootUri, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Assertions.Expect(secondPage.GetByRole(AriaRole.Heading, new() { Name = "Test sign-in" })).ToBeVisibleAsync();
        await secondPage.CloseAsync();
    }

    /// <summary>
    /// Trace: FR1, TR3, NF2.
    /// Verifies: opening a protected route in a fresh browser page prompts for sign-in again even when the browser still holds a valid platform session cookie.
    /// Expected: after a seeded viewer signs in on one page, opening `/status` in a new page within the same browser context lands on the test sign-in page.
    /// Why: the app must require an explicit sign-in prompt whenever it is first opened, including direct navigation to protected routes.
    /// </summary>
    [Fact]
    public async Task StatusPage_ShouldPromptForSignIn_WhenAuthenticatedBrowserOpensProtectedRouteInNewPage()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var statusUri = new Uri(app.GetEndpoint("web"), "/status").ToString();
        await Page.GotoAsync(
            new Uri(app.GetEndpoint("web"), "/authentication/sign-in?user=local-viewer&returnUrl=%2Fstatus").ToString(),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync();

        var secondPage = await Context.NewPageAsync();
        await secondPage.GotoAsync(statusUri, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Assertions.Expect(secondPage.GetByRole(AriaRole.Heading, new() { Name = "Test sign-in" })).ToBeVisibleAsync();
        await secondPage.CloseAsync();
    }

    /// <summary>
    /// Trace: FR3, FR10, TR3, SR1.
    /// Verifies: a signed-in user with no platform role is routed to the dedicated access-denied experience.
    /// Expected: signing in as local-norole lands on the access-denied page instead of protected operator content.
    /// Why: the work package explicitly allows authentication to complete for pre-provisioned no-role users while still denying platform access.
    /// </summary>
    [Fact]
    public async Task LandingPage_ShouldRouteNoRoleUserToAccessDenied_WhenNoRoleUserSignsIn()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await Page.GotoAsync(
            new Uri(app.GetEndpoint("web"), "/authentication/sign-in?user=local-norole&returnUrl=%2F").ToString(),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Access denied" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Trace: FR7, FR9, TR2, SR1.
    /// Verifies: the administrator can reach the dedicated administrator-only UI surface after acquiring the admin scope.
    /// Expected: signing in as local-admin with the admin scope reaches the authentication administration page.
    /// Why: the higher-privilege role boundary must be proven in the browser, not only through direct API calls.
    /// </summary>
    [Fact]
    public async Task AuthenticationAdministrationPage_ShouldRenderForAdministrator_WhenAdminSignsInWithAdminScope()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await Page.GotoAsync(
            new Uri(app.GetEndpoint("web"), "/authentication/sign-in?user=local-admin&returnUrl=%2Fadministration%2Fauthentication&scope=platform.admin").ToString(),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Authentication administration" })).ToBeVisibleAsync();
    }
}
