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
        await OpenAccountPreferencesWhenProviderIsUnavailableAsync();
        await Expect(Page.GetByText("Unconfigured", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Desired setting", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Last observed at IG", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("account-preferences-error")).ToBeHiddenAsync();
        await Expect(Page.GetByText("Account preferences provider is unavailable.", new() { Exact = false })).ToBeHiddenAsync();
    }

    /// <summary>
    /// Trace: account-preferences workflow navigation requirement.
    /// Verifies: the separate observed-history workflow is activated through its accessible tab.
    /// Expected: history content is visible after activation and settings content is no longer visible.
    /// Why: operators must be able to reach diagnostics without relying on visual layout or implementation selectors.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldActivateObservedHistory_WhenHistoryTabIsSelected()
    {
        await OpenAccountPreferencesWhenProviderIsUnavailableAsync();

        await Test.StepAsync("Activate the observed history tab", async () =>
        {
            var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" });
            await observedHistoryTab.FocusAsync();
            await observedHistoryTab.PressAsync("Space");
            await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" })).ToHaveAttributeAsync("aria-selected", "true");
        });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Observed history" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Account preference state" })).ToBeHiddenAsync();
    }

    /// <summary>
    /// Trace: account-preferences keyboard accessibility requirement.
    /// Verifies: the tablist supports documented arrow-key navigation.
    /// Expected: focus and selection move from Settings to Observed history with ArrowRight.
    /// Why: keyboard operators need the same workflow access as pointer users.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldNavigateTabsWithKeyboard_WhenSettingsTabHasFocus()
    {
        await OpenAccountPreferencesWhenProviderIsUnavailableAsync();

        await Test.StepAsync("Move to observed history with the keyboard", async () =>
        {
            var settingsTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Settings" });
            await settingsTab.FocusAsync();
            await settingsTab.PressAsync("ArrowRight");
        });

        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" })).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Observed history" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Trace: account-preferences history accessibility requirement.
    /// Verifies: the observed-history panel exposes a named semantic table and cursor command.
    /// Expected: the table is named by its caption and the load-older command is available in that panel.
    /// Why: history remains inspectable without coupling the browser test to row counts or generated markup.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldExposeAccessibleHistoryControls_WhenHistoryTabIsActive()
    {
        await OpenAccountPreferencesWhenProviderIsUnavailableAsync();
        var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" });
        await observedHistoryTab.FocusAsync();
        await observedHistoryTab.PressAsync("Space");

        await Expect(Page.GetByRole(AriaRole.Table, new() { Name = "Trailing stop observations, newest observed first" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Load older observations" })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Trace: account-preferences opaque cursor requirement.
    /// Verifies: the history command reflects cursor availability rather than inventing numeric paging.
    /// Expected: the command is disabled when the provider-unavailable response has no next cursor.
    /// Why: operators must see the workflow control while receiving an honest indication that no older page is available.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldDisableLoadOlderObservations_WhenNoNextCursorExists()
    {
        await OpenAccountPreferencesWhenProviderIsUnavailableAsync();
        var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" });
        await observedHistoryTab.FocusAsync();
        await observedHistoryTab.PressAsync("Space");

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Load older observations" })).ToBeDisabledAsync();
    }

    private async Task OpenAccountPreferencesWhenProviderIsUnavailableAsync()
    {
        fixture.Provider.Available = false;
        await Page.GotoAsync(new Uri(fixture.WebBaseUri, "/authentication/sign-in?returnUrl=%2Faccount-preferences").ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-operator");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/account-preferences(?:\?.*)?$"), new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("account-preferences-loading")).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Account preference state" })).ToBeVisibleAsync();
    }

    private static class Test
    {
        public static Task StepAsync(string _, Func<Task> action) => action();
    }
}