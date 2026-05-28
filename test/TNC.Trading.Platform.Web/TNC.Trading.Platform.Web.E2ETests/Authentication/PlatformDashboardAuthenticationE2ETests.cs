using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

[Collection(AuthenticationE2ETestCollection.Name)]
public sealed class PlatformDashboardAuthenticationE2ETests : PageTest
{
    private readonly RealAuthenticationE2ETestFixture fixture;

    public PlatformDashboardAuthenticationE2ETests(RealAuthenticationE2ETestFixture fixture)
    {
        this.fixture = fixture;
    }

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true
    };

    /// <summary>
    /// Trace: FR1, FR5, IR1, TR3, NF2.
    /// Verifies: the AppHost-started Web UI runtime endpoint can be discovered before a seeded viewer completes one real local Keycloak sign-in journey.
    /// Expected: the runtime-discovered Web home entry point is reachable and signing in as local-viewer reaches the protected operator home page.
    /// Why: the retained smoke must prove the real AppHost plus Keycloak path without falling back to brittle launch-settings ports, and it must fail clearly if endpoint discovery or Web readiness breaks.
    /// </summary>
    [Fact]
    public async Task OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard()
    {
        await Page.GotoAsync(new Uri(fixture.WebBaseUri, "/authentication/sign-in?returnUrl=%2Fstatus").ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-viewer");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new Regex(@"^https?://localhost:\d+(/|/status)(\?platformPrompted=1)?$"),
            new() { Timeout = 30_000 });

        if (Page.Url.Contains("/status", StringComparison.OrdinalIgnoreCase))
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Platform status" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        else
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Operational summary" })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
    }
}
