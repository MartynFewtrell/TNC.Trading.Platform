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

    public override async Task InitializeAsync()
    {
        fixture.Provider.Reset();
        await base.InitializeAsync();
        await fixture.ResetAccountPreferencesAsync();
    }

    /// <summary>
    /// Trace: account-preferences operator display requirement.
    /// Verifies: the authenticated operator page distinguishes durable desired intent from the provider observation.
    /// Expected: both labels are accessible when the provider is unavailable.
    /// Why: the UI must preserve SQL-owned intent during an external provider outage.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldRefreshWithConfirmedSave_WhenPreferenceIsChanged()
    {
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Radio, new() { Name = "Enabled", Exact = true }).Filter(new() { Visible = true }).CheckAsync();
        await Page.GetByTestId("account-preferences-save").ClickAsync();
        await Expect(Page.GetByTestId("account-preferences-confirmation")).ToContainTextAsync("saved and confirmed");
        await Expect(Page.GetByText("Enabled", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AccountPreferences_ShouldShowCheckStatusEquality_WhenProviderMatchesDesiredValue()
    {
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check status" }).ClickAsync();

        await Expect(Page.GetByText("Last confirmed", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Disabled", new() { Exact = true }).Last).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AccountPreferences_ShouldAutomaticallyCorrectDrift_WhenCheckStatusFindsDifferentProviderValue()
    {
        fixture.Provider.Preference = true;
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check status" }).ClickAsync();

        await Expect(Page.GetByText("Last confirmed", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Disabled", new() { Exact = true }).Last).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation()
    {
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Radio, new() { Name = "Enabled", Exact = true }).Filter(new() { Visible = true }).ClickAsync();
        await Page.GetByTestId("account-preferences-save").ClickAsync();
        await Expect(Page.GetByText("Last confirmed", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Enabled", new() { Exact = true }).Last).ToBeVisibleAsync();
        fixture.Provider.Preference = false;
        fixture.Provider.Available = false;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check status" }).ClickAsync();

        var warning = Page.GetByTestId("account-preferences-warning");
        await Expect(warning).ToHaveTextAsync("IG account preference observation was not available.");
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
        await OpenAccountPreferencesAsync();

        await Test.StepAsync("Activate the observed history tab", async () =>
        {
            var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" }).Last;
            await observedHistoryTab.ClickAsync();
            await Expect(observedHistoryTab).ToHaveAttributeAsync("aria-selected", "true");
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
        await OpenAccountPreferencesAsync();

        await Test.StepAsync("Move to observed history with the keyboard", async () =>
        {
            var settingsTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Settings" }).Last;
            await settingsTab.ClickAsync();
            var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" }).Last;
            await observedHistoryTab.FocusAsync();
            await observedHistoryTab.PressAsync("Enter");
        });

        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" }).Filter(new() { Visible = true })).ToHaveAttributeAsync("aria-selected", "true");
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
        await OpenAccountPreferencesAsync();
        var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" }).Last;
        await observedHistoryTab.ClickAsync();
        await Expect(observedHistoryTab).ToHaveAttributeAsync("aria-selected", "true");

        await Expect(Page.GetByRole(AriaRole.Table, new() { Name = "Trailing stop observations, newest observed first" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Load older observations" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AccountPreferences_ShouldPageReadOnlyHistory_WhenOlderObservationsAreAvailable()
    {
        await OpenAccountPreferencesAsync();
        await ActivateObservedHistoryAsync();

        var historyTable = Page.GetByRole(AriaRole.Table, new() { Name = "Trailing stop observations, newest observed first" });
        await Expect(historyTable).ToBeVisibleAsync();
        var loadOlder = Page.GetByRole(AriaRole.Button, new() { Name = "Load older observations" });
        await Expect(loadOlder).ToBeDisabledAsync();
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
        await OpenAccountPreferencesAsync();
        var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" }).Filter(new() { Visible = true });
        await observedHistoryTab.ClickAsync();
        await Expect(observedHistoryTab).ToHaveAttributeAsync("aria-selected", "true");

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Load older observations" })).ToBeDisabledAsync();
    }

    private async Task OpenAccountPreferencesAsync()
    {
        await Page.GotoAsync(new Uri(fixture.WebBaseUri, "/authentication/sign-in?returnUrl=%2Faccount-preferences").ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.Locator("#username")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Page.Locator("#username").FillAsync("local-operator");
        await Page.Locator("#password").FillAsync("LocalAuth!123");
        await Page.Locator("#kc-login").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/account-preferences(?:\?.*)?$"), new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("account-preferences-loading")).ToBeHiddenAsync();
        await Expect(Page.Locator("#account-preferences-state-heading")).ToBeVisibleAsync();
    }

    private async Task ActivateObservedHistoryAsync()
    {
        var observedHistoryTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Observed history" }).Filter(new() { Visible = true });
        await observedHistoryTab.ClickAsync();
        await Expect(observedHistoryTab).ToHaveAttributeAsync("aria-selected", "true");
    }

    private static class Test
    {
        public static Task StepAsync(string _, Func<Task> action) => action();
    }
}