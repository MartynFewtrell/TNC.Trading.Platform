using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

public class PlatformAuthenticationE2ETests : PageTest
{
    static PlatformAuthenticationE2ETests()
    {
        Environment.SetEnvironmentVariable("AppHost__EnableInfrastructureContainers", bool.FalseString);
    }

    /// <summary>
    /// Trace: FR1, FR2, TR3, OR2.
    /// Verifies: the browser can reach the local sign-in entry surface used for automated authentication flows.
    /// Expected: navigating to the sign-in route shows the local test sign-in choices.
    /// Why: the end-to-end suite needs a stable auth entry surface before it can validate later signed-in flows.
    /// </summary>
    [Fact]
    public async Task SignInPage_ShouldShowTestUserChoices_WhenAnonymousBrowserRequestsSignIn()
    {
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await Page.GotoAsync(new Uri(app.GetEndpoint("web"), "/authentication/sign-in?returnUrl=%2Fstatus").ToString());

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Test sign-in" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("local-viewer")).ToBeVisibleAsync();
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

        await Page.GotoAsync(new Uri(app.GetEndpoint("web"), "/authentication/sign-in?user=local-norole&returnUrl=%2F").ToString());

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

        await Page.GotoAsync(new Uri(app.GetEndpoint("web"), "/authentication/sign-in?user=local-admin&returnUrl=%2Fadministration%2Fauthentication&scope=platform.admin").ToString());

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Authentication administration" })).ToBeVisibleAsync();
    }
}
