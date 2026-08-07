using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class ConfigurationTests
{
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

        var cut = context.RenderComponent<Configuration>();

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
            _ => PlatformWebTestData.CreateProblemResponse(
                HttpStatusCode.BadRequest,
                new
                {
                    errors = new Dictionary<string, string[]>
                    {
                        ["ChangedBy"] = ["ChangedBy is required."]
                    }
                }));

        var cut = context.RenderComponent<Configuration>();
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
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, PlatformWebTestData.CreateConfiguration(restartRequired: true)));

        var cut = context.RenderComponent<Configuration>();
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

        var cut = context.RenderComponent<Configuration>();

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

        var cut = context.RenderComponent<Configuration>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='configuration-credential-reentry-message']")));
    }
}
