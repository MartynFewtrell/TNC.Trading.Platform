using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class ConfigurationTests
{
    private static readonly Guid DemoBrokerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task LoadAsync_ShouldReturnMappedForm_WhenConfigurationLoads()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()));
        var presenter = context.Services.GetRequiredService<ConfigurationPagePresenter>();

        var result = await presenter.LoadAsync(CancellationToken.None);

        Assert.NotNull(result.Form);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("08:00", result.Form.StartOfDayText);
        Assert.Equal("Monday,Tuesday,Wednesday,Thursday,Friday", result.Form.TradingDaysCsv);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 4.
    /// Verifies: the Operator configuration surface displays current quota use, safe failure status, and a paused state when allowance is unset.
    /// Expected: used/allowed counts are visible when configured and no allowance is described as paused.
    /// Why: collection must never imply a provider-call allowance that an Operator has not approved.
    /// </summary>
    [Fact]
    public void Render_ShouldShowQuotaAndPausedState_WhenCollectionAllowanceIsConfiguredOrMissing()
    {
        using var configuredContext = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()));
        var configured = configuredContext.Render<Configuration>();
        configured.WaitForAssertion(() =>
        {
            Assert.Contains("Daily request quota: 4 used of 25; 21 remaining.", configured.Markup, StringComparison.Ordinal);
            Assert.Contains("Latest collection state: Partial", configured.Markup, StringComparison.Ordinal);
            Assert.Contains("Collection needs attention: ProviderUnavailable", configured.Markup, StringComparison.Ordinal);
        });

        using var pausedContext = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration(allowance: null)),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()));
        var paused = pausedContext.Render<Configuration>();
        paused.WaitForAssertion(() =>
        {
            Assert.Contains("Collector: Paused", paused.Markup, StringComparison.Ordinal);
            Assert.Contains("leaving it unset also keeps collection paused", paused.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 4.
    /// Verifies: changing frequency and allowance in the existing form submits both new settings through the protected configuration API.
    /// Expected: the PUT body contains the edited numeric values and the page renders the updated configuration response.
    /// Why: collection settings must use the existing Operator save flow and must not initiate provider refresh.
    /// </summary>
    [Fact]
    public void Save_ShouldSubmitInstrumentFrequencyAndAllowance_WhenOperatorEditsCollectionSettings()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration(
                currentFrequency: 1,
                pendingFrequency: 3,
                allowance: 40)));

        var cut = context.Render<Configuration>();
        cut.WaitForElement("[data-testid='instrument-updates-per-day']");
        cut.Find("[data-testid='instrument-updates-per-day']").Change(3);
        cut.Find("[data-testid='approved-daily-request-allowance']").Change(40);
        cut.Find("[data-testid='configuration-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("\"instrumentUpdatesPerDay\":3", context.ApiHandler.Requests[3].Content, StringComparison.Ordinal);
            Assert.Contains("\"approvedNonTradingDailyRequestAllowance\":40", context.ApiHandler.Requests[3].Content, StringComparison.Ordinal);
            Assert.Contains("Daily request quota: 4 used of 40; 36 remaining.", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Equal(HttpMethod.Put, context.ApiHandler.Requests[3].Method);
    }

    [Fact]
    public async Task SaveAsync_ShouldReturnRestartMessage_WhenProtectedSaveRequiresRestart()
    {
        using var context = PlatformComponentTestContext.CreateServiceContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration(restartRequired: true)));
        var presenter = context.Services.GetRequiredService<ConfigurationPagePresenter>();
        var loadResult = await presenter.LoadAsync(CancellationToken.None);

        var result = await presenter.SaveAsync(loadResult.Form!, CancellationToken.None);

        Assert.NotNull(result.UpdatedForm);
        Assert.Equal("Configuration saved. Startup-fixed changes apply on the next platform start.", result.Message);
    }

    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the refreshed configuration page opens the environment accordion by default while keeping later sections collapsed initially.
    /// Expected: the first accordion section renders with the `open` attribute and the trading-schedule section does not.
    /// Why: the lower-level component suite should protect the WP004 default presentation behavior without requiring browser navigation.
    /// </summary>
    [Fact]
    public void Render_ShouldOpenEnvironmentAccordionByDefault_WhenConfigurationLoads()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()));

        var cut = context.Render<Configuration>();

        cut.WaitForAssertion(() =>
        {
            var sections = cut.FindAll("details.platform-accordion-section");
            Assert.True(sections[0].HasAttribute("open"));
            Assert.False(sections[1].HasAttribute("open"));
        });
    }

    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the configuration page preserves in-progress edits when the protected save request fails.
    /// Expected: the edited field value remains in the form and the save failure message is rendered.
    /// Why: operators must not lose unsaved changes when the protected API returns a validation or transport failure.
    /// </summary>
    [Fact]
    public void Save_ShouldPreserveEditedValue_WhenProtectedSaveFails()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()),
            _ => PlatformWebTestData.CreateProblemResponse(
                HttpStatusCode.BadRequest,
                new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["ChangedBy"] = ["ChangedBy is required."]
                    }
                }));

        var cut = context.Render<Configuration>();
        cut.WaitForElement("[data-testid='configuration-save-button']");
        var changedByInput = cut.FindAll("input").Last();
        changedByInput.Change("updated-operator");

        cut.Find("[data-testid='configuration-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("updated-operator", cut.FindAll("input").Last().GetAttribute("value"));
            Assert.Contains("Configuration save failed", cut.Find("[data-testid='configuration-save-message']").TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: FR3, NF2, TR1, OR1.
    /// Verifies: the configuration page surfaces the operator-facing restart message when the protected save response indicates startup-fixed changes remain pending.
    /// Expected: saving successfully renders the restart-required save message.
    /// Why: the component suite should protect the operator guidance emitted by the lower-level save path.
    /// </summary>
    [Fact]
    public void Save_ShouldShowRestartMessage_WhenProtectedSaveRequiresRestart()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration(restartRequired: true)));

        var cut = context.Render<Configuration>();
        cut.WaitForElement("[data-testid='configuration-save-button']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Startup-fixed changes apply on the next platform start.", cut.Find("[data-testid='configuration-save-message']").TextContent, StringComparison.Ordinal));
    }

    /// <summary>
    /// Traces to DR-02 and DR-03. Verifies an unreadable credential projection gives one complete
    /// re-entry instruction while retaining write-only blank secret inputs and rendering no secret value.
    /// Expected: the stable remediation message is present and all new credential inputs are blank.
    /// Why: operators must repair the whole protected credential set without receiving stored secret material.
    /// </summary>
    [Fact]
    public void Render_ShouldRequireAllDemoCredentials_WhenCredentialReentryIsRequired()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration(requiresCredentialReentry: true)));

        var cut = context.Render<Configuration>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("API key, identifier, and password together", cut.Find("[data-testid='configuration-credential-reentry-message']").TextContent, StringComparison.Ordinal);
            Assert.All(
                ["configuration-new-api-key", "configuration-new-identifier", "configuration-new-password"],
                testId => Assert.True(string.IsNullOrEmpty(cut.Find($"[data-testid='{testId}']").GetAttribute("value"))));
            Assert.DoesNotContain("secret-value", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Traces to DR-02 and DR-03. Verifies the remediation instruction is conditional on the safe aggregate flag.
    /// Expected: a fully usable credential projection does not render the re-entry message.
    /// Why: healthy operators should not be prompted to rotate valid credentials.
    /// </summary>
    [Fact]
    public void Render_ShouldHideCredentialReentryMessage_WhenCredentialsAreUsable()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()));

        var cut = context.Render<Configuration>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='configuration-credential-reentry-message']")));
    }

    /// <summary>
    /// Trace: Phase 3 selection acknowledgement. Verifies the selection action remains disabled until the operator explicitly acknowledges restart semantics.
    /// Expected: the available broker is selected from the applied status, but the action is disabled before acknowledgement.
    /// Why: a broker change must not be submitted accidentally when it only takes effect after restart.
    /// </summary>
    [Fact]
    public void BrokerSelection_ShouldRequireAcknowledgement_WhenAppliedBrokerIsLoaded()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()));

        var cut = context.Render<Configuration>();

        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("[data-testid='configuration-select-broker-button']");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal("Applied broker: IG Demo", cut.Find("[data-testid='configuration-applied-broker']").TextContent);
        });
    }

    /// <summary>
    /// Trace: broker selection configuration.
    /// Verifies: retired broker environments are excluded from the selectable environment list.
    /// Expected: active broker environments remain visible, while retired records are absent.
    /// Why: retired environments cannot be selected and must not be presented as available choices.
    /// </summary>
    [Fact]
    public void BrokerSelection_ShouldHideRetiredEnvironment_WhenBrokerCatalogContainsRetiredEnvironment()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalogWithRetiredEnvironment()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus()));

        var cut = context.Render<Configuration>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("IG Demo (Demo)", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Retired IG Demo", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: Phase 3 pending selection. Verifies the rendered status distinguishes an unapplied selected broker and exposes the restart warning.
    /// Expected: applied and selected names differ and the pending indicator is rendered.
    /// Why: operators need an unambiguous view of whether a selection is merely pending or already applied.
    /// </summary>
    [Fact]
    public void BrokerSelection_ShouldShowPendingState_WhenSelectedBrokerDiffersFromAppliedBroker()
    {
        using var context = new PlatformComponentTestContext(
            "local-operator",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerCatalog()),
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, CreateBrokerStatus(restartRequired: true, selectedName: "IG Demo Next")));

        var cut = context.Render<Configuration>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Applied broker: IG Demo", cut.Find("[data-testid='configuration-applied-broker']").TextContent);
            Assert.Equal("Selected broker: IG Demo Next", cut.Find("[data-testid='configuration-selected-broker']").TextContent);
            Assert.Contains("Pending broker selection will apply", cut.Find("[data-testid='configuration-restart-required-indicator']").TextContent, StringComparison.Ordinal);
        });
    }

    private static BrokerEnvironmentViewModel[] CreateBrokerCatalog() =>
    [
        new(
            DemoBrokerId,
            "IG Demo",
            "IG",
            "Demo",
            "Active",
            "Available",
            null,
            "Demo",
            true,
            true,
            "catalog-token")
    ];

    private static BrokerEnvironmentViewModel[] CreateBrokerCatalogWithRetiredEnvironment() =>
    [
    .. CreateBrokerCatalog(),
    new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "Retired IG Demo",
        "IG",
        "Demo",
        "Retired",
        "Unavailable",
        "Retired by an administrator.",
        "Demo",
        true,
        false,
        "retired-catalog-token")
    ];

    private static BrokerEnvironmentStatusViewModel CreateBrokerStatus(bool restartRequired = false, string? selectedName = null)
    {
        var applied = CreateBrokerCatalog()[0];
        var selected = selectedName is null ? applied : applied with { Name = selectedName };
        return new("Test", applied, selected, restartRequired, 1);
    }
}
