using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.AccountPreferences;

[Collection(Authentication.AuthenticationE2ETestCollection.Name)]
public sealed class AccountPreferencesE2ETests : PageTest
{
    private readonly Authentication.RealAuthenticationE2ETestFixture fixture;

    public AccountPreferencesE2ETests(Authentication.RealAuthenticationE2ETestFixture fixture)
    {
        this.fixture = fixture;
    }

    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true };

    /// <summary>
    /// Trace: account-preferences operator display requirement.
    /// Verifies: the authenticated operator page distinguishes durable desired intent from the provider observation.
    /// Expected: both labels are accessible when the provider is unavailable.
    /// Why: the UI must preserve SQL-owned intent during an external provider outage.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable()
    {
        fixture.Provider.Available = false;
        await Page.GotoAsync(new Uri(fixture.WebBaseUri, "/authentication/sign-in?returnUrl=%2Faccount-preferences").ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-operator");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/account-preferences(?:\?.*)?$"), new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("account-preferences-loading")).ToBeHiddenAsync();
        await Expect(Page.GetByText("Unconfigured", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Desired setting", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Last observed at IG", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("account-preferences-error")).ToBeHiddenAsync();
        await Expect(Page.GetByText("Account preferences provider is unavailable.", new() { Exact = false })).ToBeHiddenAsync();
    }
}