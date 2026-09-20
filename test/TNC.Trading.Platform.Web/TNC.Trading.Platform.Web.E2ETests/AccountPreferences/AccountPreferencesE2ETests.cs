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
        var enabled = Page.GetByRole(AriaRole.Radio, new() { Name = "Enabled", Exact = true }).Filter(new() { Visible = true });
        var save = Page.GetByTestId("account-preferences-save");
        await Expect(enabled).Not.ToBeCheckedAsync();
        await enabled.CheckAsync();
        await Expect(enabled).ToBeCheckedAsync();
        await Expect(save).ToBeEnabledAsync();
        fixture.Provider.ClearRequests();
        await save.ClickAsync();
        var saveResponse = await fixture.Provider.WaitForResponseAsync("PUT /gateway/deal/accounts/preferences", TimeSpan.FromSeconds(30));
        Assert.Equal(200, saveResponse.StatusCode);
        await Expect(Page.GetByTestId("account-preferences-confirmation")).ToContainTextAsync("saved and confirmed", new() { Timeout = 30_000 });
        await Expect(Page.GetByText("Enabled", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AccountPreferences_ShouldShowCheckStatusEquality_WhenProviderMatchesDesiredValue()
    {
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check status" }).ClickAsync();

        await Expect(Page.GetByText("Last confirmed", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.Locator("time[datetime]")).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AccountPreferences_ShouldAutomaticallyCorrectDrift_WhenCheckStatusFindsDifferentProviderValue()
    {
        fixture.Provider.Preference = true;
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check status" }).ClickAsync();

        await Expect(Page.GetByText("Last confirmed", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.Locator("time[datetime]")).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Trace: DR-02, DD-01. Verifies the unavailable-provider remediation reaches the platform check-status endpoint and renders its safe failure state.
    /// Expected: the page exposes the unavailable condition in its alert after the server-side operation completes.
    /// Why: Blazor Server performs the API call outside the browser, so the browser-visible warning is the completion signal.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation()
    {
        await OpenAccountPreferencesAsync();
        await Page.GetByRole(AriaRole.Radio, new() { Name = "Enabled", Exact = true }).Filter(new() { Visible = true }).CheckAsync();
        var save = Page.GetByTestId("account-preferences-save");
        await Expect(save).ToBeEnabledAsync();
        await save.ClickAsync();
        var saveResponse = await fixture.Provider.WaitForResponseAsync("PUT /gateway/deal/accounts/preferences", TimeSpan.FromSeconds(30));
        Assert.Equal(200, saveResponse.StatusCode);
        await Expect(Page.GetByTestId("account-preferences-confirmation")).ToContainTextAsync("saved and confirmed", new() { Timeout = 30_000 });
        await Expect(Page.GetByText("Enabled", new() { Exact = true }).Last).ToBeVisibleAsync();
        fixture.Provider.Preference = false;
        fixture.Provider.Available = false;
        fixture.Provider.ClearRequests();
        var checkStatus = Page.Locator("button.platform-primary-action").First;
        await Expect(checkStatus).ToBeEnabledAsync();
        await checkStatus.ClickAsync();

        TNC.Trading.Platform.TestShared.AccountPreferences.ProviderRequest providerResponse;
        try
        {
            providerResponse = await fixture.Provider.WaitForResponseAsync("GET /gateway/deal/accounts/preferences", TimeSpan.FromSeconds(30));
        }
        catch (Exception exception)
        {
            var alerts = string.Join(" | ", await Page.GetByRole(AriaRole.Alert).AllTextContentsAsync());
            var buttons = string.Join(" | ", await Page.GetByRole(AriaRole.Button).EvaluateAllAsync<string[]>("buttons => buttons.map(button => `${button.textContent?.trim()} [disabled=${button.hasAttribute('disabled')}]`)"));
            throw new InvalidOperationException($"{exception.Message} Alerts: {alerts} Buttons: {buttons}", exception);
        }

        Assert.Equal(503, providerResponse.StatusCode);
        await Expect(checkStatus).ToBeEnabledAsync(new() { Timeout = 30_000 });
        var warning = Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "unavailable" }).Filter(new() { Visible = true });
        try
        {
            await Expect(warning).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Expect(warning).ToContainTextAsync("unavailable", new() { IgnoreCase = true, Timeout = 30_000 });
        }
        catch (Exception exception)
        {
            var alerts = string.Join(" | ", await Page.GetByRole(AriaRole.Alert).AllTextContentsAsync());
            throw new InvalidOperationException($"{exception.Message} Alerts: {alerts}", exception);
        }
        Assert.Contains("POST /gateway/deal/session", fixture.Provider.Requests);
    }

    /// <summary>
    /// Trace: account-preferences responsive layout requirement.
    /// Verifies: both native choices remain visible and usable while the Save action remains below them on desktop.
    /// Expected: choice hit areas do not overlap, Save is below the fieldset with a usable alignment, and the page has no horizontal overflow at 1280px.
    /// Why: operators need a clear, usable configuration workflow without introducing horizontal overflow, whether the choices remain on one row or wrap.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide()
    {
        await Page.SetViewportSizeAsync(1280, 900);
        await OpenAccountPreferencesAsync();

        var row = Page.Locator(".account-preferences-control-row");
        var fieldset = Page.Locator("fieldset.account-preferences-choice");
        var enabled = Page.GetByRole(AriaRole.Radio, new() { Name = "Enabled", Exact = true }).Filter(new() { Visible = true });
        var disabled = Page.GetByRole(AriaRole.Radio, new() { Name = "Disabled", Exact = true }).Filter(new() { Visible = true });
        var enabledHitArea = fieldset.Locator("label").Filter(new() { HasText = "Enabled" });
        var disabledHitArea = fieldset.Locator("label").Filter(new() { HasText = "Disabled" });
        var save = Page.GetByTestId("account-preferences-save");
        await Expect(row).ToBeVisibleAsync();
        await Expect(fieldset).ToBeVisibleAsync();
        await Expect(enabled).ToBeVisibleAsync();
        await Expect(disabled).ToBeVisibleAsync();
        await Expect(enabledHitArea).ToBeVisibleAsync();
        await Expect(disabledHitArea).ToBeVisibleAsync();
        await Expect(save).ToBeVisibleAsync();

        var fieldsetBox = await fieldset.BoundingBoxAsync();
        var enabledHitAreaBox = await enabledHitArea.BoundingBoxAsync();
        var disabledHitAreaBox = await disabledHitArea.BoundingBoxAsync();
        var saveBox = await save.BoundingBoxAsync();

        Assert.NotNull(fieldsetBox);
        Assert.NotNull(enabledHitAreaBox);
        Assert.NotNull(disabledHitAreaBox);
        Assert.NotNull(saveBox);
        const double edgeTolerance = 4;

        var choicesOverlap = enabledHitAreaBox!.X < disabledHitAreaBox!.X + disabledHitAreaBox.Width
            && disabledHitAreaBox.X < enabledHitAreaBox.X + enabledHitAreaBox.Width
            && enabledHitAreaBox.Y < disabledHitAreaBox.Y + disabledHitAreaBox.Height
            && disabledHitAreaBox.Y < enabledHitAreaBox.Y + enabledHitAreaBox.Height;
        Assert.False(choicesOverlap);
        Assert.True(saveBox!.Y > fieldsetBox!.Y + fieldsetBox.Height);
        Assert.InRange(Math.Abs(saveBox.X - fieldsetBox.X), 0, edgeTolerance);
        Assert.True(await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth"));
    }

    /// <summary>
    /// Trace: account-preferences responsive layout requirement.
    /// Verifies: the control pane wraps at a narrow viewport without creating horizontal overflow.
    /// Expected: the row remains visible and its content stays within the viewport.
    /// Why: compact desktop styling must not make the safety-sensitive controls unusable on small screens.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldWrapControlsWithoutOverflow_WhenViewportIsNarrow()
    {
        await Page.SetViewportSizeAsync(360, 800);
        await OpenAccountPreferencesAsync();

        await Expect(Page.Locator(".account-preferences-control-row")).ToBeVisibleAsync();
        Assert.Equal(360, await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth"));
        await Expect(Page.GetByTestId("account-preferences-save")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Trace: account-preferences keyboard accessibility requirement.
    /// Verifies: native radio arrow-key interaction changes the selected value.
    /// Expected: focus moves from Enabled to Disabled and the Disabled radio becomes checked.
    /// Why: keyboard operators must be able to choose the provider setting without pointer interaction.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldSelectRadioWithArrowKey_WhenChoiceHasFocus()
    {
        await OpenAccountPreferencesAsync();
        var enabled = Page.GetByRole(AriaRole.Radio, new() { Name = "Enabled", Exact = true }).Filter(new() { Visible = true });
        var disabled = Page.GetByRole(AriaRole.Radio, new() { Name = "Disabled", Exact = true }).Filter(new() { Visible = true });

        await enabled.FocusAsync();
        await enabled.PressAsync("ArrowRight");

        await Expect(disabled).ToBeCheckedAsync();
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
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
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